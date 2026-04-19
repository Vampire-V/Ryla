#!/usr/bin/env python3
"""
LINE Messaging API stub server สำหรับ E2E tests
รัน: python3 tests/e2e/line_stub_server.py [port]

รองรับ:
  POST /v2/bot/message/push                      -> 200 OK (record push)
  GET  /__received                               -> JSON list ของ pushes ที่ได้รับ
  POST /__reset                                  -> ล้าง received list
  GET  /__fail_mode?code=401&message=Unauthorized -> ตั้งค่าให้ push endpoint return error
  GET  /__delay?ms=15000                         -> ตั้งค่าให้ push endpoint sleep (timeout test)

Endpoint behavior:
  - push endpoint ตรวจสอบ Authorization header (Bearer token)
  - Record (token, body) ทุกครั้งที่ได้รับ
"""
import json
import os
import sys
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs


_received = []
_fail_code = None
_fail_message = None
_delay_ms = 0


class LineStubHandler(BaseHTTPRequestHandler):
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
        global _fail_code, _fail_message

        parsed = urlparse(self.path)

        if parsed.path == "/__reset":
            _received.clear()
            _fail_code = None
            _fail_message = None
            self._send(200, {"ok": True})
            return

        if parsed.path == "/v2/bot/message/push":
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
                "token": auth.replace("Bearer ", "") if auth.startswith("Bearer ") else None,
                "body": payload,
            })

            if _fail_code is not None:
                self._send(_fail_code, {"message": _fail_message or "stub error"})
                return

            self._send(200, {})
            return

        self._send(404, {"message": "not found"})

    def do_GET(self):
        global _fail_code, _fail_message, _delay_ms

        parsed = urlparse(self.path)
        params = parse_qs(parsed.query)

        if parsed.path == "/__received":
            self._send(200, _received)
            return

        if parsed.path == "/__fail_mode":
            code = params.get("code", [None])[0]
            _fail_code = int(code) if code else None
            _fail_message = params.get("message", [None])[0]
            self._send(200, {"fail_code": _fail_code, "message": _fail_message})
            return

        if parsed.path == "/__delay":
            ms = params.get("ms", [None])[0]
            _delay_ms = int(ms) if ms else 0
            self._send(200, {"delay_ms": _delay_ms})
            return

        self._send(404, {"message": "not found"})


def run(port=9999):
    server = HTTPServer(("127.0.0.1", port), LineStubHandler)
    print(f"LINE stub server listening on http://127.0.0.1:{port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nshutting down", flush=True)
        server.server_close()


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else int(os.environ.get("LINE_STUB_PORT", "9999"))
    run(port)
