import json
import pathlib
import subprocess
import unittest


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[5]
CONTAINER_REQUIREMENTS = (
    REPOSITORY_ROOT / "Payload_Type/athena/.docker/requirements.txt"
)
CONTAINER_DOCKERFILE = REPOSITORY_ROOT / "Payload_Type/athena/.docker/Dockerfile"
BUILDER = (
    REPOSITORY_ROOT
    / "Payload_Type/athena/athena/mythic/agent_functions/builder.py"
)
DOWNLOAD_BROWSER_SCRIPT = (
    REPOSITORY_ROOT
    / "Payload_Type/athena/athena/mythic/browser_scripts/download.js"
)
REMOTE_DOCKERFILE = REPOSITORY_ROOT / "Payload_Type/athena/Dockerfile"
CONFIG = REPOSITORY_ROOT / "config.json"
CONTAINER_WORKFLOW = REPOSITORY_ROOT / ".github/workflows/docker.yml"
V4_CONTAINER_VERSION = "mythic-container==0.7.0rc9"
V4_IMAGE = "ghcr.io/mythicagents/athena:Mythic-v4.0.0"


def render_download(task, responses):
    harness = """
const fs = require("fs");
const source = fs.readFileSync(process.argv[1], "utf8");
const render = eval("(" + source + ")");
const task = JSON.parse(process.argv[2]);
const responses = JSON.parse(process.argv[3]);
process.stdout.write(JSON.stringify(render(task, responses)));
"""
    completed = subprocess.run(
        [
            "node",
            "-e",
            harness,
            str(DOWNLOAD_BROWSER_SCRIPT),
            json.dumps(task),
            json.dumps(responses),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    return json.loads(completed.stdout)


class MythicV4CompatibilityTests(unittest.TestCase):
    def test_container_library_is_pinned_to_v4_release(self):
        requirements = CONTAINER_REQUIREMENTS.read_text().splitlines()
        dockerfile = CONTAINER_DOCKERFILE.read_text()

        self.assertIn(V4_CONTAINER_VERSION, requirements)
        self.assertNotIn("mythic-container\n", CONTAINER_REQUIREMENTS.read_text())
        self.assertIn(V4_CONTAINER_VERSION, dockerfile)
        self.assertNotIn(" mythic-container ", dockerfile)

    def test_payload_definition_does_not_publish_removed_wrapper_allowlist(self):
        self.assertNotIn("wrapped_payloads", BUILDER.read_text())

    def test_completed_download_uses_authenticated_media_renderer(self):
        result = render_download(
            {"status": "completed", "completed": True},
            ["registered", json.dumps({"file_id": "file-123"})],
        )

        self.assertNotIn("download", result)
        self.assertEqual(
            [{"agent_file_id": "file-123", "editable": False}],
            result["media"],
        )

    def test_download_renderer_tolerates_partial_and_error_responses(self):
        partial = render_download(
            {"status": "processed", "completed": False},
            [json.dumps({"totalChunks": 4})],
        )
        error = render_download(
            {"status": "error", "completed": True},
            ["first", "second"],
        )
        malformed = render_download(
            {"status": "completed", "completed": True},
            ["not-json"],
        )

        self.assertEqual(
            {"plaintext": "Downloading a file with 4 total chunks..."},
            partial,
        )
        self.assertEqual({"plaintext": "firstsecond"}, error)
        self.assertEqual({"plaintext": "not-json"}, malformed)

    def test_v4_branch_builds_and_advertises_its_own_container_image(self):
        workflow = CONTAINER_WORKFLOW.read_text()
        config = json.loads(CONFIG.read_text())

        self.assertIn("- Mythic-v4.0.0", workflow)
        self.assertIn("push: ${{ github.ref_type == 'tag' || github.ref_name == 'Mythic-v4.0.0' }}", workflow)
        self.assertIn("if: ${{ github.ref_type == 'tag' }}", workflow)
        self.assertEqual(V4_IMAGE, config["remote_images"]["athena"])
        self.assertEqual("FROM " + V4_IMAGE, REMOTE_DOCKERFILE.read_text().strip())

    def test_container_workflow_reuses_build_cache(self):
        workflow = CONTAINER_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("cache-from: type=gha,scope=athena-container", workflow)
        self.assertIn(
            "cache-to: type=gha,mode=max,scope=athena-container", workflow
        )


if __name__ == "__main__":
    unittest.main()
