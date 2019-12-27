# agones-udp-server-csharp

## Docker

```shell
agones_sdk_version=1.2.1
docker build -t agones-udp-server-csharp:${agones_sdk_version} -f src/Agones/Dockerfile .
docker tag agones-udp-server-csharp:${agones_sdk_version} guitarrapc/agones-udp-server-csharp:${agones_sdk_version}
docker push guitarrapc/agones-udp-server-csharp:${agones_sdk_version}
```

## Deploy and try

Install Agones Controller 1.2.0.

deploy fleet to Agones.

```
kubectl apply -f ./k8s
kubectl get fleet
```

try nc to 1 of GameServer.

```
$ nc -u 192.168.65.3 7553
```