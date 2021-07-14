FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim

COPY /certificates /certificates

RUN openssl x509 -inform DER -in /certificates/root.cer -out /certificates/root.crt \
    && openssl x509 -inform DER -in /certificates/int.cer -out /certificates/int.crt \
    && cp /certificates/root.crt /usr/local/share/ca-certificates/ \
    && cp /certificates/int.crt /usr/local/share/ca-certificates/ \
    && update-ca-certificates

ADD https://aka.ms/dotnet-counters/linux-x64 /root/dotnet-counters

RUN chmod +x /root/dotnet-counters

COPY out/CertMemLeak /app

WORKDIR /app

ENTRYPOINT ["dotnet", "CertMemLeak.dll", "/certificates/client.cer", "PT2H"]
