#!/usr/bin/env python3
"""Exports the NetworkServer fast lane (Redis stream networkserver_fast, ~20k entries = a few hours) to JSONL for `ReplayHarness fast`.
Read-only; the Redis password is taken from the worker container's env and never printed.
  python3 export-fast-lane.py > entries.jsonl"""
import json, re, subprocess, sys
cs = subprocess.check_output(["docker", "exec", "asset360-dev-worker-network-1", "printenv", "Redis__ConnectionString"], text=True)
pw = re.search(r"password=([^,;]*)", cs, re.I).group(1)
out = subprocess.check_output(["docker", "exec", "-e", f"REDISCLI_AUTH={pw}", "asset360-dev-redis-dev-1", "redis-cli", "--json", "XRANGE", "networkserver_fast", "-", "+"], text=True)
for _id, fields in json.loads(out):
    d = dict(zip(fields[::2], fields[1::2]))
    print(json.dumps({"id": _id, "gw": d.get("gw"), "recv": int(d["recv"]), "r": json.loads(d["r"])}))
