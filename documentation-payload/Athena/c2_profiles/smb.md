+++
title = "smb"
chapter = false
weight = 5
+++

## Summary


The athena agent can act as an SMB Client, sending messages through any supported SMB Server profiles.

Note: SMB agents do not perform a check-in process, as such their last check-in time will tick upwards in the Mythic UI.

However, if the agent is still alive the egress agent will perform the check-in on behalf of the SMB agent, and will pass the task along.

The SMB agent will then reply with the task output as normal.

### Profile Option Deviations

This profile makes no deviations from the standard `smb` profile provided.