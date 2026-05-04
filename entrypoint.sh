#!/bin/bash
ln -sf /usr/lib/x86_64-linux-gnu/libtommath.so.1 /usr/lib/x86_64-linux-gnu/libtommath.so.0 2>/dev/null
export FIREBIRD=/opt/firebird
export HOME_MEMORY_FBCLIENT=/opt/firebird/lib/libfbclient.so.2
export HOME_MEMORY_DB_PATH=/data/homememory.scd
export HOME_MEMORY_TRANSPORT=http
export HOME_MEMORY_PORT=5100
exec dotnet /app/HomeMemoryMCP.dll
