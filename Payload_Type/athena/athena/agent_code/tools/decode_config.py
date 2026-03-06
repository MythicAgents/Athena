#!/usr/bin/env python3
"""Decode XOR-obfuscated configs from Athena agent payloads.

Supports three input modes:
  --binary   Scan a compiled binary for all XOR-encoded JSON configs
  --source   Parse a generated .g.cs file
  --hex/--key  Decode manually provided data
"""
import argparse
import json
import re
import sys


def xor_decode(data: bytes, key: int) -> bytes:
    return bytes(b ^ key for b in data)


def try_json(data: bytes) -> str | None:
    try:
        text = data.decode("utf-8")
        if text.strip().startswith("{"):
            json.loads(text)
            return text
    except (UnicodeDecodeError, json.JSONDecodeError):
        pass
    return None


def decode_from_source(path: str) -> list[str]:
    with open(path, "r") as f:
        content = f.read()

    byte_match = re.search(
        r"new byte\[\]\s*\{([^}]+)\}", content
    )
    key_match = re.search(
        r"readonly byte _k\s*=\s*(0x[0-9A-Fa-f]{2})", content
    )

    if not byte_match or not key_match:
        print("Could not find byte array or key in source file.",
              file=sys.stderr)
        return []

    hex_values = re.findall(r"0x[0-9A-Fa-f]{2}", byte_match.group(1))
    data = bytes(int(h, 16) for h in hex_values)
    key = int(key_match.group(1), 16)

    decoded = xor_decode(data, key)
    result = try_json(decoded)
    return [result] if result else []


def decode_from_hex(hex_str: str, key: int) -> list[str]:
    hex_values = re.findall(r"0x[0-9A-Fa-f]{2}", hex_str)
    if not hex_values:
        hex_values = hex_str.strip().split(",")

    data = bytes(int(h.strip(), 16) for h in hex_values)
    decoded = xor_decode(data, key)
    result = try_json(decoded)
    return [result] if result else []


def decode_from_binary(path: str) -> list[str]:
    with open(path, "rb") as f:
        blob = f.read()

    results = []
    target = ord("{")

    for offset in range(len(blob)):
        candidate_key = blob[offset] ^ target
        if candidate_key == 0:
            continue

        end = None
        depth = 0
        in_string = False
        escape_next = False
        for i in range(offset, min(offset + 65536, len(blob))):
            ch = blob[i] ^ candidate_key
            if ch > 127:
                break
            c = chr(ch)
            if escape_next:
                escape_next = False
                continue
            if c == '\\' and in_string:
                escape_next = True
                continue
            if c == '"':
                in_string = not in_string
                continue
            if in_string:
                continue
            if c == '{':
                depth += 1
            elif c == '}':
                depth -= 1
                if depth == 0:
                    end = i + 1
                    break

        if end is None:
            continue

        segment = blob[offset:end]
        decoded = xor_decode(segment, candidate_key)
        result = try_json(decoded)
        if result and result not in results:
            results.append(result)

    return results


def main():
    parser = argparse.ArgumentParser(
        description="Decode XOR-obfuscated Athena agent configs"
    )
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument(
        "--binary", metavar="PATH",
        help="Scan compiled binary for encoded configs"
    )
    group.add_argument(
        "--source", metavar="PATH",
        help="Parse a .g.cs source file"
    )
    group.add_argument(
        "--hex", metavar="BYTES",
        help="Hex byte string (e.g. '0x4A,0x2F,...')"
    )
    parser.add_argument(
        "--key", metavar="BYTE", type=lambda x: int(x, 0),
        help="XOR key byte (required with --hex)"
    )
    args = parser.parse_args()

    if args.hex and args.key is None:
        parser.error("--key is required when using --hex")

    if args.source:
        results = decode_from_source(args.source)
    elif args.hex:
        results = decode_from_hex(args.hex, args.key)
    else:
        results = decode_from_binary(args.binary)

    if not results:
        print("No configs found.", file=sys.stderr)
        sys.exit(1)

    for i, r in enumerate(results):
        if len(results) > 1:
            print(f"--- Config {i + 1} ---")
        print(json.dumps(json.loads(r), indent=2))


if __name__ == "__main__":
    main()
