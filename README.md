# SURIMI Fisheries Authothiority

The fisheries authority model provides regulations and consumes catches and Fishing Activities. It is used to determine the catch limits for each species and to ensure that the fishing activities are sustainable.

## Blob storage startup

After loading Vault secrets, the service initializes blob storage before it starts listening. If `AWS_ACCESS_KEY_ID` is set, the configured S3 endpoint and bucket must pass the startup bucket check. If that check fails, the process exits without accepting gRPC requests. Check `AWS_S3_ENDPOINT`, `AWS_BUCKET_NAME`, network connectivity, and bucket permissions before restarting. If `AWS_ACCESS_KEY_ID` is not set, the service uses local storage and does not contact S3.

## Build Docker image
```bash
dotnet build /t:BuildPushDockerImage -v:detailed
```
