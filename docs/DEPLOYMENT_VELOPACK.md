# Guia de Deploy e Auto-Update com Velopack

Este documento descreve o processo de empacotamento, publicação e atualização da aplicação SelectML utilizando o Velopack.

## Pré-requisitos

1.  **.NET SDK 8.0** ou superior.
2.  **Velopack CLI (`vpk`)**.

### Instalação da Ferramenta vpk

Execute o seguinte comando no terminal para instalar a ferramenta `vpk` globalmente:

```bash
dotnet tool install -g vpk
```

## Passo a Passo para Release

### 1. Publicar a Aplicação

Compile e publique a aplicação em modo `Release` para gerar os binários. Certifique-se de estar na raiz do repositório ou navegue até o projeto `SelectML.Client`.

```bash
dotnet publish SelectML.Client/SelectML.Client.csproj -c Release --self-contained -r win-x64 -o ./publish
```

*Nota: O Velopack funciona melhor com aplicações self-contained ou framework-dependent, mas a consistência do ambiente self-contained é preferível.*

### 2. Criar o Pacote (Release)

Utilize o comando `vpk pack` para gerar o instalador (`Setup.exe`) e os arquivos de atualização (`nupkg`, `RELEASES`).

Substitua `1.0.0` pela versão desejada (Semantic Versioning).

```bash
vpk pack --packId SelectML --packVersion 1.0.0 --packDir ./publish --mainExe SelectML.Client.exe
```

Isso gerará uma pasta `Releases` contendo:
*   `SelectML-1.0.0-win-x64-Setup.exe`: Instalador para usuários novos.
*   `SelectML-1.0.0-win-x64-full.nupkg`: Pacote de atualização.
*   `RELEASES`: Arquivo de manifesto para atualizações.

### 3. Publicar Atualização

Para disponibilizar a atualização:
1.  Faça o upload do conteúdo da pasta `Releases` para o seu servidor web ou bucket S3 (configurado em `AppConfig.UpdateUrl`).
2.  Certifique-se de que o arquivo `RELEASES` seja substituído ou atualizado corretamente no servidor.

## Estrutura de Pastas Esperada no Servidor

```
/updates/
    ├── RELEASES
    ├── SelectML-1.0.0-win-x64-full.nupkg
    ├── SelectML-1.0.0-win-x64-Setup.exe
    ├── SelectML-1.0.1-win-x64-full.nupkg
    └── SelectML-1.0.1-win-x64-delta.nupkg (opcional, se gerado)
```

## Notas Importantes

*   **Configuração de URL**: A URL onde os arquivos serão hospedados deve corresponder à propriedade `UpdateUrl` definida em `AppConfig` (ou `appsettings.json`).
*   **Versionamento**: Sempre incremente a versão (ex: 1.0.0 -> 1.0.1) ao gerar um novo pacote. O Velopack usa isso para detectar novidades.
*   **Delta Updates**: O Velopack pode gerar atualizações delta (menores) automaticamente se a versão anterior estiver presente na pasta de saída.

## Testando Atualizações Localmente

1.  Gere a versão 1.0.0 e instale.
2.  Gere a versão 1.0.1.
3.  Aponte o `UpdateUrl` no `appsettings.json` local (da versão instalada) para a pasta `Releases` local (usando protocolo `file:///C:/caminho/Releases`) ou suba um servidor local (ex: `python -m http.server`).
4.  Reinicie a aplicação e observe o ícone de atualização no rodapé.
