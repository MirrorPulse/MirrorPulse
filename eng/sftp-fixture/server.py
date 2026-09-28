"""Loopback SSH/SFTP fixture for process-level Adapter tests."""

import base64
import hashlib
import json
import socket
import threading
import time

import paramiko


class Authentication(paramiko.ServerInterface):
    def check_auth_password(self, username, password):
        if username == "user" and password == "correct-secret":
            return paramiko.AUTH_SUCCESSFUL
        return paramiko.AUTH_FAILED

    def get_allowed_auths(self, username):
        return "password"

    def check_channel_request(self, kind, channel_id):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED


def serve_connection(connection, host_key):
    transport = paramiko.Transport(connection)
    try:
        transport.add_server_key(host_key)
        transport.set_subsystem_handler("sftp", paramiko.SFTPServer)
        transport.start_server(server=Authentication())
        while transport.is_active():
            time.sleep(0.05)
    finally:
        transport.close()


def main():
    host_key = paramiko.RSAKey.generate(2048)
    fingerprint = base64.b64encode(hashlib.sha256(host_key.asbytes()).digest()).decode("ascii").rstrip("=")
    listener = socket.socket()
    listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    listener.bind(("127.0.0.1", 0))
    listener.listen(8)
    print(json.dumps({"port": listener.getsockname()[1], "sha256": fingerprint}), flush=True)
    while True:
        connection, _ = listener.accept()
        threading.Thread(target=serve_connection, args=(connection, host_key), daemon=True).start()


if __name__ == "__main__":
    main()
