import asyncio
import errno
import os
import signal
import subprocess
import sys

PROCESS_TERMINATE_TIMEOUT = 2.0
PROCESS_EXECUTION_TIMEOUT = 15 * 60
PROCESS_OUTPUT_LIMIT = 1024 * 1024
PROCESS_READ_CHUNK_SIZE = 64 * 1024


def _linux_descendant_pids(process_id):
    """Snapshot descendants, including children that created new sessions."""
    if not os.path.isdir("/proc"):
        return set()
    descendants = set()
    pending = [process_id]
    while pending:
        parent = pending.pop()
        try:
            task_ids = os.listdir(f"/proc/{parent}/task")
        except OSError:
            continue
        for task_id in task_ids:
            try:
                with open(
                    f"/proc/{parent}/task/{task_id}/children",
                    encoding="ascii",
                ) as children_file:
                    children = {
                        int(child) for child in children_file.read().split()
                    }
            except (OSError, ValueError):
                continue
            new_children = children - descendants
            descendants.update(new_children)
            pending.extend(new_children)
    descendants.discard(process_id)
    return descendants


def _pid_exists(process_id):
    try:
        os.kill(process_id, 0)
    except ProcessLookupError:
        return False
    except OSError as error:
        return error.errno != getattr(errno, "ESRCH", 3)
    if sys.platform.startswith("linux"):
        try:
            with open(f"/proc/{process_id}/stat", encoding="ascii") as stat_file:
                stat = stat_file.read()
        except (OSError, UnicodeError):
            return True
        if stat.rpartition(") ")[2].startswith("Z "):
            return False
    return True


def _linux_pipe_holder_pids(process):
    """Find processes retaining this subprocess's stdout/stderr pipes."""
    if not os.path.isdir("/proc"):
        return set()
    pipe_targets = set()
    for stream in (getattr(process, "stdout", None), getattr(process, "stderr", None)):
        transport = getattr(stream, "_transport", None)
        pipe = transport.get_extra_info("pipe") if transport is not None else None
        if pipe is None:
            continue
        try:
            target = os.readlink(f"/proc/self/fd/{pipe.fileno()}")
        except (AttributeError, OSError, ValueError):
            continue
        if target.startswith("pipe:["):
            pipe_targets.add(target)

    holders = set()
    if not pipe_targets:
        return holders
    try:
        process_ids = os.listdir("/proc")
    except OSError:
        return holders
    excluded = {os.getpid(), getattr(process, "pid", None)}
    for process_id_text in process_ids:
        if not process_id_text.isdigit():
            continue
        process_id = int(process_id_text)
        if process_id in excluded:
            continue
        try:
            descriptors = os.listdir(f"/proc/{process_id}/fd")
        except OSError:
            continue
        for descriptor in descriptors:
            try:
                target = os.readlink(f"/proc/{process_id}/fd/{descriptor}")
            except OSError:
                continue
            if target in pipe_targets:
                holders.add(process_id)
                break
    return holders


async def _wait_bounded(process, timeout):
    try:
        await asyncio.wait_for(process.wait(), timeout)
        return True
    except (ProcessLookupError, asyncio.TimeoutError):
        return False


async def _wait_for_leader_exit(process, timeout):
    """Observe leader exit without letting inherited pipes block Process.wait."""
    wait_task = asyncio.create_task(process.wait())
    deadline = asyncio.get_running_loop().time() + timeout
    try:
        # Let Process.wait observe already-finished fake/platform processes
        # before consulting returncode directly.
        await asyncio.sleep(0)
        while True:
            if wait_task.done():
                await wait_task
                return True
            if process.returncode is not None:
                return True
            remaining = deadline - asyncio.get_running_loop().time()
            if remaining <= 0:
                return False
            await asyncio.sleep(min(0.01, remaining))
    finally:
        if not wait_task.done():
            wait_task.cancel()
            await asyncio.gather(wait_task, return_exceptions=True)


async def _run_taskkill(process_id, force):
    command = ["taskkill", "/PID", str(process_id)]
    if force:
        command.append("/F")
    command.append("/T")
    helper = await asyncio.create_subprocess_exec(
        *command,
        stdout=asyncio.subprocess.DEVNULL,
        stderr=asyncio.subprocess.DEVNULL,
    )
    timed_out = not await _wait_bounded(helper, PROCESS_TERMINATE_TIMEOUT)
    if timed_out:
        if helper.returncode is None:
            helper.terminate()
        if not await _wait_bounded(helper, PROCESS_TERMINATE_TIMEOUT):
            if helper.returncode is None:
                helper.kill()
            if not await _wait_bounded(helper, PROCESS_TERMINATE_TIMEOUT):
                raise subprocess.TimeoutExpired(command, PROCESS_TERMINATE_TIMEOUT)
        raise subprocess.TimeoutExpired(command, PROCESS_TERMINATE_TIMEOUT)
    return_code = helper.returncode
    if return_code is None:
        raise subprocess.TimeoutExpired(command, PROCESS_TERMINATE_TIMEOUT)
    if return_code != 0:
        raise subprocess.CalledProcessError(return_code, command)


async def _signal_process_tree(process, force, escaped_descendants=()):
    if os.name == "posix" and hasattr(process, "pid"):
        process_signal = signal.SIGKILL if force else signal.SIGTERM
        signal_error = None
        try:
            os.killpg(process.pid, process_signal)
        except ProcessLookupError:
            pass
        except OSError as error:
            signal_error = error
        for descendant in escaped_descendants:
            try:
                os.kill(descendant, process_signal)
            except ProcessLookupError:
                pass
            except OSError as error:
                signal_error = error
        if signal_error is not None:
            raise signal_error
        return
    if os.name == "nt" and hasattr(process, "pid"):
        await _run_taskkill(process.pid, force)
        return
    try:
        if process.returncode is None:
            process.kill() if force else process.terminate()
    except (OSError, ProcessLookupError, asyncio.TimeoutError):
        pass


async def terminate_process_tree(process, escaped_descendants=()):
    """Fail closed on POSIX: freeze, discover, then hard-kill the tree.

    A catchable TERM lets a handler fork and reparent an untracked process. Stop
    the tree first so the discovery snapshot cannot mutate, then use SIGKILL.
    Windows retains its explicit taskkill soft/forced sequence.
    """
    escaped_descendants = set(escaped_descendants)
    cleanup_error = None
    surviving_descendants = set()
    if os.name == "posix" and hasattr(process, "pid"):
        try:
            os.killpg(process.pid, signal.SIGSTOP)
        except ProcessLookupError:
            pass
        except OSError as error:
            cleanup_error = error
        for descendant in escaped_descendants:
            try:
                os.kill(descendant, signal.SIGSTOP)
            except ProcessLookupError:
                pass
            except OSError as error:
                cleanup_error = error

        # The original group is now frozen. Freeze any separate-session
        # descendants too, repeating until the ancestry snapshot is stable.
        while True:
            discovered = _linux_descendant_pids(process.pid)
            for descendant in tuple(escaped_descendants):
                discovered.update(_linux_descendant_pids(descendant))
            new_descendants = discovered - escaped_descendants
            if not new_descendants:
                break
            escaped_descendants.update(new_descendants)
            for descendant in new_descendants:
                try:
                    os.kill(descendant, signal.SIGSTOP)
                except ProcessLookupError:
                    pass
                except OSError as error:
                    cleanup_error = error

        try:
            await _signal_process_tree(process, True, escaped_descendants)
        except OSError as error:
            cleanup_error = error
        leader_reaped = await _wait_bounded(process, PROCESS_TERMINATE_TIMEOUT)
        kill_deadline = asyncio.get_running_loop().time() + PROCESS_TERMINATE_TIMEOUT
        while (
            any(_pid_exists(descendant) for descendant in escaped_descendants)
            and asyncio.get_running_loop().time() < kill_deadline
        ):
            remaining = kill_deadline - asyncio.get_running_loop().time()
            await asyncio.sleep(min(0.01, max(0, remaining)))
        surviving_descendants = {
            descendant
            for descendant in escaped_descendants
            if _pid_exists(descendant)
        }
    else:
        soft_cleanup_failed = False
        try:
            await _signal_process_tree(process, False)
        except (
            OSError,
            subprocess.CalledProcessError,
            subprocess.TimeoutExpired,
        ):
            soft_cleanup_failed = True
        leader_reaped = await _wait_bounded(process, PROCESS_TERMINATE_TIMEOUT)
        if soft_cleanup_failed or not leader_reaped:
            await _signal_process_tree(process, True)

    if not leader_reaped:
        leader_reaped = await _wait_bounded(process, PROCESS_TERMINATE_TIMEOUT)
    if cleanup_error is not None:
        raise cleanup_error
    if (
        os.name == "posix"
        and hasattr(process, "pid")
        and not leader_reaped
    ) or surviving_descendants:
        raise subprocess.TimeoutExpired(
            getattr(process, "pid", "process"), PROCESS_TERMINATE_TIMEOUT
        )


async def _read_stream_bounded(stream, retained, limit):
    try:
        while True:
            chunk = await stream.read(PROCESS_READ_CHUNK_SIZE)
            if not chunk:
                return
            remaining = limit - len(retained)
            if remaining > 0:
                retained.extend(chunk[:remaining])
    except asyncio.CancelledError:
        # StreamReader has no public close API. Closing its read transport is
        # necessary when another process still owns the pipe's write end.
        transport = getattr(stream, "_transport", None)
        if transport is not None:
            transport.close()
        raise


async def _reader_tasks_remain_open(reader_tasks):
    """Return whether any pipe reader misses the bounded EOF window."""
    _, pending = await asyncio.wait(
        reader_tasks, timeout=PROCESS_TERMINATE_TIMEOUT
    )
    return bool(pending)


async def _finish_reader_tasks_bounded(reader_tasks):
    """Give pipe readers a bounded chance to reach EOF, then cancel them."""
    _, pending = await asyncio.wait(
        reader_tasks, timeout=PROCESS_TERMINATE_TIMEOUT
    )
    for task in pending:
        task.cancel()
    if pending:
        await asyncio.wait(pending, timeout=PROCESS_TERMINATE_TIMEOUT)

    # Retrieve exceptions from every finished reader without allowing a reader
    # that ignores cancellation to hold cleanup open indefinitely.
    for task in reader_tasks:
        if task.done() and not task.cancelled():
            task.exception()


async def _cleanup_process_and_readers(process, reader_tasks):
    try:
        await terminate_process_tree(process)
    finally:
        await _finish_reader_tasks_bounded(reader_tasks)


async def _cleanup_cancellation_safe(process, reader_tasks):
    cleanup_task = asyncio.create_task(
        _cleanup_process_and_readers(process, reader_tasks)
    )
    cancellation_received = False
    while not cleanup_task.done():
        try:
            await asyncio.shield(cleanup_task)
        except asyncio.CancelledError:
            cancellation_received = True
    if cancellation_received:
        if not cleanup_task.cancelled():
            cleanup_task.exception()
        raise asyncio.CancelledError
    cleanup_task.result()


async def run_checked(command, cwd):
    """Run argv with bounded output and time, raising with diagnostics on failure."""
    group_options = {}
    if os.name == "posix":
        group_options["start_new_session"] = True
    elif os.name == "nt" and hasattr(subprocess, "CREATE_NEW_PROCESS_GROUP"):
        group_options["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP

    try:
        process = await asyncio.create_subprocess_exec(
            *command,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
            cwd=cwd,
            **group_options,
        )
    except OSError as error:
        setattr(error, "command", command)
        raise

    stdout_retained = bytearray()
    stderr_retained = bytearray()
    stdout_task = asyncio.create_task(
        _read_stream_bounded(process.stdout, stdout_retained, PROCESS_OUTPUT_LIMIT)
    )
    stderr_task = asyncio.create_task(
        _read_stream_bounded(process.stderr, stderr_retained, PROCESS_OUTPUT_LIMIT)
    )
    reader_tasks = (stdout_task, stderr_task)
    try:
        exited = await _wait_for_leader_exit(process, PROCESS_EXECUTION_TIMEOUT)
        if not exited:
            raise asyncio.TimeoutError
    except asyncio.TimeoutError:
        await _cleanup_cancellation_safe(process, reader_tasks)
        raise subprocess.TimeoutExpired(
            command,
            PROCESS_EXECUTION_TIMEOUT,
            output=bytes(stdout_retained).decode(errors="replace"),
            stderr=bytes(stderr_retained).decode(errors="replace"),
        )
    except asyncio.CancelledError:
        await _cleanup_cancellation_safe(process, reader_tasks)
        raise
    if (
        os.name == "posix"
        and sys.platform.startswith("linux")
        and hasattr(process, "pid")
    ):
        pipe_holders = _linux_pipe_holder_pids(process)
        if pipe_holders:
            await terminate_process_tree(process, pipe_holders)
    elif await _reader_tasks_remain_open(reader_tasks):
        # Windows has no /proc equivalent here, and non-Linux POSIX cannot
        # discover descendants that leave the original session. Once the
        # bounded EOF window proves a writer survived its leader, invoke the
        # native tree/group cleanup rather than only closing our read ends.
        await terminate_process_tree(process)
    await _finish_reader_tasks_bounded(reader_tasks)

    stdout = bytes(stdout_retained).decode(errors="replace")
    stderr = bytes(stderr_retained).decode(errors="replace")
    return_code = process.returncode
    assert return_code is not None
    if return_code != 0:
        raise subprocess.CalledProcessError(
            return_code,
            command,
            output=stdout,
            stderr=stderr,
        )
    return stdout, stderr
