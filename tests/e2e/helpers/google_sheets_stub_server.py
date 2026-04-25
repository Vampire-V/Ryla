#!/usr/bin/env python3
"""
Google Sheets API + OAuth2 token stub server สำหรับ E2E tests
รัน: python3 tests/e2e/helpers/google_sheets_stub_server.py [port]

รองรับ:
  POST /token                                       -> 200 + fake access_token (จำลอง OAuth2 token endpoint)
  POST /v4/spreadsheets/{id}/values/{range}:append  -> 200 OK (record append)
  GET  /__received                                  -> JSON list ของ append calls
  POST /__reset                                     -> ล้าง received list + reset fail mode
  GET  /__fail_mode?endpoint=append&code=429        -> ตั้งค่า endpoint ใด endpoint หนึ่ง fail
                  (endpoint = append | token)
  GET  /__delay?ms=15000                            -> ตั้งค่า append endpoint sleep (timeout test)

Endpoint behavior:
  - /token รับ form-urlencoded body, return access_token TTL 3600
  - append endpoint ตรวจสอบ Authorization header (Bearer token), record body
"""
import json
import os
import sys
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs


_received = []
_token_calls = 0
_fail = {"append": None, "token": None}
_fail_message = None
_delay_ms = 0


class SheetsStubHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def _send(self, status, payload):
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        global _token_calls

        parsed = urlparse(self.path)

        if parsed.path == "/__reset":
            _received.clear()
            _token_calls = 0
            _fail["append"] = None
            _fail["token"] = None
            self._send(200, {"ok": True})
            return

        # OAuth2 token endpoint
        if parsed.path == "/token":
            length = int(self.headers.get("Content-Length", "0"))
            self.rfile.read(length)
            _token_calls += 1
            if _fail["token"] is not None:
                self._send(_fail["token"], {"error": "invalid_grant", "error_description": _fail_message or "stub"})
                return
            self._send(200, {
                "access_token": "stub-access-token-" + str(_token_calls),
                "expires_in": 3600,
                "token_type": "Bearer",
            })
            return

        # Sheets append endpoint: /v4/spreadsheets/{id}/values/{range}:append
        if parsed.path.startswith("/v4/spreadsheets/") and ":append" in parsed.path:
            length = int(self.headers.get("Content-Length", "0"))
            body = self.rfile.read(length).decode("utf-8") if length else ""
            auth = self.headers.get("Authorization", "")

            if _delay_ms > 0:
                time.sleep(_delay_ms / 1000.0)

            try:
                payload = json.loads(body) if body else {}
            except json.JSONDecodeError:
                payload = {"_raw": body}

            _received.append({
                "path": parsed.path,
                "query": parsed.query,
                "token": auth.replace("Bearer ", "") if auth.startswith("Bearer ") else None,
                "body": payload,
            })

            if _fail["append"] is not None:
                self._send(_fail["append"], {"error": {"code": _fail["append"], "message": _fail_message or "stub error"}})
                return

            self._send(200, {
                "spreadsheetId": "stub-spreadsheet-id",
                "updates": {"updatedRows": 1, "updatedCells": len(payload.get("values", [[]])[0])},
            })
            return

        self._send(404, {"message": "not found"})

    def do_GET(self):
        global _fail_message, _delay_ms

        parsed = urlparse(self.path)
        params = parse_qs(parsed.query)

        if parsed.path == "/__received":
            self._send(200, _received)
            return

        if parsed.path == "/__token_calls":
            self._send(200, {"count": _token_calls})
            return

        if parsed.path == "/__fail_mode":
            endpoint = params.get("endpoint", ["append"])[0]
            code = params.get("code", [None])[0]
            if endpoint not in ("append", "token"):
                self._send(400, {"error": "endpoint must be append or token"})
                return
            _fail[endpoint] = int(code) if code else None
            _fail_message = params.get("message", [None])[0]
            self._send(200, {"endpoint": endpoint, "code": _fail[endpoint], "message": _fail_message})
            return

        if parsed.path == "/__delay":
            ms = params.get("ms", [None])[0]
            _delay_ms = int(ms) if ms else 0
            self._send(200, {"delay_ms": _delay_ms})
            return

        self._send(404, {"message": "not found"})


def run(port=9998):
    server = HTTPServer(("127.0.0.1", port), SheetsStubHandler)
    print(f"Google Sheets stub server listening on http://127.0.0.1:{port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nshutting down", flush=True)
        server.server_close()


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else int(os.environ.get("SHEETS_STUB_PORT", "9998"))
    run(port)
