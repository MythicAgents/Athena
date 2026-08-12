+++
title = "zoom"
chapter = false
weight = 7
+++

## Summary

The athena agent can communicate with Mythic over a **Zoom Team Chat** channel
using the `zoom` C2 profile. It uses Zoom's REST API with headless
Server-to-Server OAuth, so no end-user browser or in-meeting SDK is required —
the agent exchanges encrypted blobs with Mythic through a private Team Chat
channel.

This is an application-layer channel that blends with normal Zoom Team Chat
traffic (sanctioned SaaS chat is frequently allow-listed and exempt from TLS
inspection). It is distinct from media-plane techniques like TURNt / "Ghost
Calls".

## How it works

* The agent mints a per-instance correlation id.
* On checkin and every beacon tick, it AES-encrypts its `checkin` /
  `get_tasking` payload (`ICryptoManager.Encrypt`) and posts it to the channel
  as one or more small JSON envelopes (`t = "O"`), chunked to stay under Zoom's
  4096-character message limit.
* The Mythic-side `zoom` C2 profile polls the channel, reassembles complete
  jobs, and forwards the opaque base64 to Mythic (`POST` to `MYTHIC_ADDRESS`
  with the header `Mythic: zoom`).
* Mythic's encrypted response is chunked and posted back (`t = "I"`) addressed
  to the agent's correlation id.
* A background receiver in the agent polls the channel, reassembles responses,
  decrypts them, and dispatches `GetTaskingResponse` tasking to the agent core.
* Both sides delete the messages they consume (burn-after-read).

Crypto is end-to-end between the agent and Mythic — the wire only carries
opaque base64.

## Setup (Zoom side)

1. Create a **Server-to-Server OAuth** app in the
   [Zoom App Marketplace](https://marketplace.zoom.us/).
2. Grant scopes: `team_chat:read:user_message` (list/read messages) and
   `team_chat:write:user_message` (send + delete messages), plus a Team Chat
   channel scope to list channels (any `team_chat:read:*` channel scope) for the
   channel-id lookup.
3. Record the **Account ID**, **Client ID**, and **Client Secret**.
4. Create a **private** Team Chat channel and add the S2S app's user as a
   member. Record the **channel id**.
5. Install and start the `zoom` C2 profile, and fill in the same credentials in
   its `c2_code/config.json`.

## Parameters

| Parameter | Description |
|---|---|
| `zoom_account_id` | S2S OAuth Account ID |
| `client_id` | S2S OAuth Client ID |
| `client_secret` | S2S OAuth Client Secret |
| `user_id` | Zoom user id to act as (`me`) |
| `channel_id` | Private Team Chat channel id used as the bus |
| `api_base` | Zoom REST base URL (override for a redirector) |
| `oauth_base` | Zoom OAuth base URL (override for a redirector) |
| `callback_interval` | Beacon sleep in seconds |
| `callback_jitter` | Beacon jitter in percent |
| `killdate` | Agent kill date |
| `encrypted_exchange_check` | Perform Mythic key exchange |
| `AESPSK` | Crypto type (`aes256_hmac` / `none`) |

## Notes

* The agent solicits tasking every `callback_interval` (it always sends a
  `get_tasking`, even with no queued responses) and a background receiver polls
  the channel every few seconds for responses.
* Because the channel is asynchronous, checkin/beacon latency is bounded by the
  bridge poll interval plus the agent receiver poll interval (a few seconds
  each).
* `client_secret` is baked into the compiled payload — use a dedicated
  low-privilege Zoom service account.
* Large transfers are chunked into ~3000-char base64 fragments; very large
  transfers produce many messages.
