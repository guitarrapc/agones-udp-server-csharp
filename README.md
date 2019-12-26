# agones-udp-server-csharp

## Docker

```shell
docker_version=0.2.1
docker build -t agones-udp-server-csharp:${docker_version} -f src/Agones/Dockerfile .
docker tag agones-udp-server-csharp:${docker_version} guitarrapc/agones-udp-server-csharp:${docker_version}
docker push guitarrapc/agones-udp-server-csharp:${docker_version}
```