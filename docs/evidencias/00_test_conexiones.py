import os
import re
import json
import socket
import sys
import time
from urllib.parse import urlparse

import redis
import pika
import requests

HERE = os.path.dirname(os.path.abspath(__file__))
KEYS_FILE = os.path.normpath(os.path.join(HERE, "..", "..", "..", "APIKEYS (NO COMPARTIR).txt"))
OUT_FILE = os.path.join(HERE, "00_conexiones.txt")

def mask(url):
    if not url:
        return url
    p = urlparse(url)
    if p.password:
        return url.replace(p.password, "***")
    return url

def load_keys():
    if not os.path.exists(KEYS_FILE):
        print(f"ERROR: no se encontro {KEYS_FILE}", file=sys.stderr)
        sys.exit(2)
    txt = open(KEYS_FILE, encoding="utf-8").read()
    redis_url = re.search(r"redis-cli -u (\S+)", txt)
    amqp_url = re.search(r"(amqps?://\S+)", txt)
    piesocket_api = re.search(r"PieSocket Apikey:\s*(\S+)", txt)
    piesocket_cluster = re.search(r"PieSocket Cluster ID:\s*(\S+)", txt)
    piesocket_secret = re.search(r"PieSocket api secret:\s*(\S+)", txt)
    algolia_app = re.search(r"AlGOLIA Application ID:\s*(\S+)", txt)
    algolia_key = re.search(r"Algolia Apikey:\s*(\S+)", txt)
    return {
        "redis": redis_url.group(1) if redis_url else None,
        "amqp": amqp_url.group(1) if amqp_url else None,
        "piesocket_api": piesocket_api.group(1) if piesocket_api else None,
        "piesocket_cluster": piesocket_cluster.group(1) if piesocket_cluster else None,
        "piesocket_secret": piesocket_secret.group(1) if piesocket_secret else None,
        "algolia_app": algolia_app.group(1) if algolia_app else None,
        "algolia_key": algolia_key.group(1) if algolia_key else None,
    }

def test_redis(conn):
    print("== REDIS ==", flush=True)
    r = redis.Redis.from_url(conn, socket_timeout=10, socket_connect_timeout=10)
    pong = r.ping()
    print("PING:", pong, flush=True)
    test_key = "fase0:conexion:test"
    r.set(test_key, "ok", ex=60)
    val = r.get(test_key)
    print("SET/GET:", val.decode() if isinstance(val, bytes) else val, flush=True)
    r.delete(test_key)
    info = r.info("server")
    print(f"redis_version={info['redis_version']} mode={info['redis_mode']}", flush=True)
    return True

def test_amqp(conn):
    print("== CLOUDAMQP ==", flush=True)
    params = pika.URLParameters(conn)
    params.socket_timeout = 10
    conn_ = pika.BlockingConnection(params)
    ch = conn_.channel()
    ch.confirm_delivery()
    queue = "solicitudes.notificaciones"
    ch.queue_declare(queue=queue, durable=True, passive=False)
    ch.queue_declare(queue=queue, durable=True, passive=True)
    ch.basic_publish(
        exchange="",
        routing_key=queue,
        body=json.dumps({"probe": "fase0"}),
        properties=pika.BasicProperties(delivery_mode=2),
        mandatory=True,
    )
    method, props, body = ch.basic_get(queue, auto_ack=False)
    print("Publicado y recuperado:", body.decode() if isinstance(body, bytes) else body, flush=True)
    if method is not None:
        ch.basic_ack(method.delivery_tag)
    conn_.close()
    return True

def test_piesocket(keys):
    print("== PIESOCKET ==", flush=True)
    base = f"https://{keys['piesocket_cluster']}.piesocket.com/api/publish"
    r = requests.post(
        base,
        json={
            "key": keys["piesocket_api"],
            "secret": keys["piesocket_secret"],
            "channelId": "fase0_conexion_test",
            "message": {"probe": "fase0", "ts": int(time.time())},
        },
        timeout=15,
    )
    print("HTTP", r.status_code, r.text[:300], flush=True)
    return r.status_code == 200

def test_algolia(keys):
    print("== ALGOLIA ==", flush=True)
    r = requests.post(
        f"https://{keys['algolia_app']}-dsn.algolia.net/1/indexes/solicitudes/query",
        headers={
            "X-Algolia-Application-Id": keys["algolia_app"],
            "X-Algolia-API-Key": keys["algolia_key"],
            "Content-Type": "application/json",
        },
        json={"query": "", "hitsPerPage": 5},
        timeout=15,
    )
    print("HTTP", r.status_code, r.text[:300], flush=True)
    return r.status_code == 200

def main():
    keys = load_keys()
    lines = [
        "PRUEBA DE CONEXIONES - Fase 0",
        "==============================",
        f"fecha: {time.strftime('%Y-%m-%d %H:%M:%S UTC', time.gmtime())}",
        "",
        f"redis url : {mask(keys['redis'])}",
        f"amqp  url : {mask(keys['amqp'])}",
        f"piesocket : cluster={keys['piesocket_cluster']} (secret enmascarado)",
        f"algolia   : app={keys['algolia_app']} (key enmascarada)",
        "",
    ]
    results = {}
    for name, fn in [
        ("redis", lambda: test_redis(keys["redis"])),
        ("amqp", lambda: test_amqp(keys["amqp"])),
        ("piesocket", lambda: test_piesocket(keys)),
        ("algolia", lambda: test_algolia(keys)),
    ]:
        try:
            results[name] = fn()
        except Exception as e:
            results[name] = False
            print(f"[{name}] ERROR: {e}", file=sys.stderr, flush=True)
    lines.append("RESULTADOS")
    lines.append("==========")
    for k, v in results.items():
        lines.append(f"{k}: {'OK' if v else 'FALLO'}")
    open(OUT_FILE, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"Evidencia guardada en {OUT_FILE}", flush=True)
    sys.exit(0 if all(results.values()) else 1)

if __name__ == "__main__":
    main()