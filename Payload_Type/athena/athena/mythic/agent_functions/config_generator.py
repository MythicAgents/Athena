import json
import random
from pathlib import Path


PROFILE_CONFIG_TARGETS = {
    "http": ("Agent.Profiles.Http", "Agent.Profiles"),
    "smb": ("Agent.Profiles.Smb", "Agent.Profiles.Smb"),
    "websocket": ("Agent.Profiles.Websocket", "Agent.Profiles.Websocket"),
    "discord": ("Agent.Profiles.Discord", "Agent.Profiles"),
    "github": ("Agent.Profiles.GitHub", "Agent.Profiles"),
    "zoom": ("Agent.Profiles.Zoom", "Agent.Profiles"),
}


def normalize_profile_config(parameters, profile_name=None):
    config = {}
    for key, value in parameters.items():
        if key == "AESPSK":
            continue
        if key == "encrypted_exchange_check":
            config[key] = value is True or str(value).upper() == "T"
        else:
            config[key] = value
    if profile_name == "zoom":
        config["account_id"] = config.pop("zoom_account_id", config.get("account_id", ""))
        config.setdefault("user_id", "me")
        config.setdefault("api_base", "https://api.zoom.us/v2")
        config.setdefault("oauth_base", "https://zoom.us/oauth")
    return config


def normalize_agent_config(payload_uuid, parameters):
    config = {
        "uuid": payload_uuid,
        "callback_interval": 60,
        "callback_jitter": 10,
        "killdate": "",
        "psk": "",
        "plugin_contract_fingerprint_required": bool(parameters.get("obfuscate", False)),
    }
    crypto = "None"
    for key, value in parameters.items():
        if key == "AESPSK":
            enc_key = value.get("enc_key") if isinstance(value, dict) else None
            config["psk"] = enc_key or ""
            crypto = "Aes" if enc_key else "None"
        elif key in ("callback_interval", "callback_jitter"):
            config[key] = int(value)
        elif key == "killdate":
            config[key] = str(value)
    return config, crypto


def render_xor_config(namespace, class_name, config, xor_key=None):
    xor_key = random.randint(1, 255) if xor_key is None else xor_key
    if not 1 <= xor_key <= 255:
        raise ValueError("xor_key must be between 1 and 255")
    json_bytes = json.dumps(config, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    encoded = bytes(value ^ xor_key for value in json_bytes)
    byte_literal = ", ".join(f"0x{value:02X}" for value in encoded)
    return f"""// Auto-generated per payload.
namespace {namespace}
{{
    internal static class {class_name}
    {{
        private static readonly byte[] _d = new byte[] {{ {byte_literal} }};
        private static readonly byte _k = 0x{xor_key:02X};

        internal static string Decode()
        {{
            byte[] result = new byte[_d.Length];
            for (int i = 0; i < _d.Length; i++)
                result[i] = (byte)(_d[i] ^ _k);
            return System.Text.Encoding.UTF8.GetString(result);
        }}
    }}
}}
"""


def write_profile_config(agent_code_root, profile_name, parameters, xor_key=None):
    directory, namespace = PROFILE_CONFIG_TARGETS[profile_name]
    output = Path(agent_code_root) / directory / "ChannelConfig.cs"
    output.write_text(render_xor_config(namespace, "ChannelConfig", normalize_profile_config(parameters, profile_name=profile_name), xor_key=xor_key), encoding="utf-8")
    return output


def write_agent_config(agent_code_root, payload_uuid, parameters, xor_key=None):
    config, crypto = normalize_agent_config(payload_uuid, parameters)
    output = Path(agent_code_root) / "AthenaCore" / "Config" / "AgentConfigData.cs"
    output.write_text(render_xor_config("Agent.Config", "AgentConfigData", config, xor_key=xor_key), encoding="utf-8")
    return output, crypto
