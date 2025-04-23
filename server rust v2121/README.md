# Servidor Rust v2121

Este repositório contém os arquivos necessários para configurar e gerenciar um servidor Rust na versão v2121. O projeto inclui executáveis, configurações, plugins e outros recursos essenciais para personalizar e administrar o servidor.

## Estrutura do Projeto

Abaixo está uma visão geral da estrutura do projeto:

- **Executáveis e DLLs**:
  - `Compiler.exe`
  - `Rust.bat`
  - `RustDedicated.exe`
  - `steam_api64.dll`
  - `steamclient64.dll`
  - `tier0_s64.dll`
  - `UnityCrashHandler64.exe`
  - `UnityPlayer.dll`
  - `vstdlib_s64.dll`

- **Diretórios Importantes**:
  - `appcache/`: Contém cache de aplicativos.
  - `Bundles/`: Inclui pacotes de itens, mapas e recursos compartilhados.
  - `config/`: Arquivos de configuração para plugins e personalizações do servidor.
  - `logs/`: Logs de atividades e eventos do servidor.
  - `MonoBleedingEdge/`: Contém bibliotecas e runtime do Mono.
  - `oxide/`: Diretório relacionado ao Oxide, incluindo configurações, dados, idiomas, logs e plugins.
  - `RustDedicated_Data/`: Arquivos de dados do servidor Rust.
  - `server/`: Diretório para dados específicos do servidor, como mundos e configurações personalizadas.
  - `steamapps/`: Contém informações sobre bibliotecas Steam.

## Configurações

Os arquivos de configuração estão localizados no diretório `config/` e incluem:

- Configurações de plugins como `BetterChat.json`, `BetterLoot.json`, `Clans.json`, entre outros.
- Arquivos de imagens e ícones, como `progressbar.png` e `timericon.png`.
- Configurações gerais do servidor, como `oxide.config.json`.

## Plugins

Os plugins estão localizados no diretório `oxide/plugins/` e permitem personalizar a jogabilidade e funcionalidades do servidor. Exemplos de plugins configuráveis incluem:

- `BetterChat`
- `Clans`
- `HeliControl`
- `Kits`
- `Vanish`
- `ZoneManager`

## Logs

Os logs do servidor estão disponíveis no diretório `logs/` e incluem informações sobre conexões, inventário, estatísticas e outros eventos importantes.

## Como Usar

1. Certifique-se de ter o SteamCMD instalado para gerenciar as atualizações do servidor.
2. Execute o arquivo `Rust.bat` para iniciar o servidor.
3. Personalize as configurações no diretório `config/` conforme necessário.
4. Adicione ou remova plugins no diretório `oxide/plugins/` para ajustar a jogabilidade.

## Requisitos

- Sistema Operacional: Windows
- Dependências: SteamCMD, Mono (incluso no projeto)

## Contribuição

Contribuições são bem-vindas! Sinta-se à vontade para abrir issues ou enviar pull requests para melhorias no projeto.

## Licença

Este projeto é distribuído sob a licença [MIT](LICENSE).