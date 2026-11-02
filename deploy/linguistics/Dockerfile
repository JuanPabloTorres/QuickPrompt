FROM debian@sha256:1e1a22175fbf1b32157ddaf0219e1db4e5401fab2b94ed285add64d18587cd2d
ENV LANG=C.UTF-8 LC_ALL=C.UTF-8 DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    apertium=3.9.12-1 apertium-eng-spa=0.8.1-2 apertium-es-pt=1.1.6-1 lttoolbox=3.8.0-2 liblttoolbox3=3.8.0-2 \
    && rm -rf /var/lib/apt/lists/*
RUN printf '%s\n' \
    '9c273f2e22c6c87e5b2c35ddc97c02e954eff01dfa355b2f9324a1bcd0b9d364  /usr/share/apertium/apertium-eng-spa/spa-eng.automorf.bin' \
    'fc6d6370bcc58043af75fe15787104f3cab8d187153752596f2a6a7fca7959ea  /usr/share/apertium/apertium-es-pt/pt-es.automorf.bin' \
    'e80ae89a37f7a08d966c2c7d6fda0c97f3bc97b501716b85c4957bca7f42978b  /usr/bin/lt-proc' \
    '79d6ec404120dc43af0c655867feeb5e64f614694e4f234e2d104d114f479528  /usr/bin/apertium' | sha256sum -c -
USER 65534:65534
