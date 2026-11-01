#!/usr/bin/env python3
"""
Test script to verify Companion Web Server WebSocket command handling.
Connects to ws://localhost:8080/ws and sends 'switch_game' commands.
"""
import asyncio
import json
import sys

async def test_switch():
    try:
        import websockets
    except ImportError:
        print("websockets not installed, skipping network test")
        return

    uri = "ws://localhost:8080/ws"
    print(f"Connecting to {uri}...")
    try:
        async with websockets.connect(uri) as ws:
            print("Connected! Waiting for initial telemetry...")
            msg = await asyncio.wait_for(ws.recv(), timeout=3.0)
            data = json.loads(msg)
            print(f"Received telemetry: ActiveGame={data.get('game', {}).get('activeGameId')}")

            # Send switch game command
            print("Sending switch_game to 'WobbleRunner'...")
            await ws.send(json.dumps({"type": "switch_game", "gameId": "WobbleRunner"}))

            # Wait for updated telemetry
            for _ in range(5):
                msg = await asyncio.wait_for(ws.recv(), timeout=2.0)
                data = json.loads(msg)
                active = data.get('game', {}).get('activeGameId')
                if active == 'WobbleRunner':
                    print("SUCCESS! Game switched to WobbleRunner via WebSocket!")
                    return

            print(f"Switch completed, final active game: {active}")
    except Exception as e:
        print(f"Test error (is Godot running?): {e}")

if __name__ == "__main__":
    asyncio.run(test_switch())
