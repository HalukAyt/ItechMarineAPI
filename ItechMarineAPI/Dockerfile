# ===============================
#   Build Stage
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Çözüm dosyasını ve bağımlılıkları kopyala
COPY . .

# Restore: proje yolu doğru olmalı
RUN dotnet restore "./src/MarineControl.Api/MarineControl.Api.csproj"

# Publish: çıktı klasörünü belirle
RUN dotnet publish "./src/MarineControl.Api/MarineControl.Api.csproj" -c Release -o /app/publish

# ===============================
#   Runtime Stage
# ===============================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Render 8080 portunu yönlendirir
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Yayınlanan dosyaları kopyala
COPY --from=build /app/publish .

# DLL adıyla birebir eşleşmeli
ENTRYPOINT ["dotnet", "MarineControl.Api.dll"]
