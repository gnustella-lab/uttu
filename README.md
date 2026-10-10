# Uttu Desktop · Copiloto Nativo de Conteúdo Social

> **100% Nativo para Windows 10 | Sem Electron / Sem WebView | Buffer MCP via Stdio | Flat Design**

O **Uttu Desktop** é um assistente autônomo local e copiloto de conteúdo social (X, LinkedIn, Threads, Bluesky, Instagram), recriado como um software de desktop 100% nativo para Windows 10 em .NET 8 e C# com WPF otimizado, inspirado diretamente no [Otto](https://otto.doti.gg/).

---

## ⚡ Principais Características

- **Consumo Mínimo de Memória RAM:** Apenas **~15 a 45 MB em standby**, inicialização instantânea em milissegundos sem qualquer sobrecarga de Chromium ou navegadores headless.
- **Protocolo MCP (Model Context Protocol) via Stdio:** Orquestra o servidor Buffer MCP usando pipes bidirecionais JSON-RPC 2.0 (`StandardInput` / `StandardOutput`) sem expor portas TCP/IP de rede.
- **Isolamento de Processos com Windows Job Objects:** Garante terminação imediata de processos filhos do MCP mesmo em caso de encerramento forçado do processo pai (zero processos órfãos).
- **Segurança com Windows DPAPI:** Buffer Access Token e chaves de API nunca são salvas em texto plano, utilizando `ProtectedData.Protect` com `DataProtectionScope.CurrentUser`.
- **Notificações Toast Interativas no Windows 10:** Disparo proativo matinal diário (ex: às 08:30) com ações interativas nativas no Action Center (`[Aprovar Agora]`, `[Abrir Uttu]`, `[Ignorar]`).
- **Geração Bi-mórfica:** Para cada tema, gera simultaneamente a versão compacta para o **X** (máximo 280 caracteres) e estruturada para o **LinkedIn** (espaçamento vertical com gancho nas primeiras linhas).
- **Gerador de Cartões 1600 × 900 com SkiaSharp:** Renderização gráfica 2D ultra-rápida (< 50ms) sem navegadores, produzindo cartões nos temas escuro (`#0C0D0E`) e claro (`#F3F4F6`).
- **Calibração de Perfil de Voz:** Extração instantânea de métricas linguísticas (tamanho de sentença, densidade de pontuação, detecção de ganchos e palavras proibidas).
- **Central de Contexto e Memórias (SQLite):** Armazenamento local rápido em `uttu.db` com modo WAL habilitado.
- **Human-in-the-Loop Obrigatório:** Nenhuma postagem é enviada sem clique explícito ou atalho acelerador (`Ctrl+Enter`).

---

## 🏛️ Arquitetura do Sistema

```
+-------------------------------------------------------------------------------+
|                       APLICATIVO WINDOWS 10 (EXE NATIVO)                      |
|                                                                               |
|  +-------------------------------------------------------------------------+  |
|  |           INTERFACE NATIVA FLAT-DESIGN (XAML / WPF)                     |  |
|  |  - Feed de Rascunhos Pendentes (VirtualizingStackPanel)                 |  |
|  |  - Painel de Edição de Memórias e Regras (SQLite)                       |  |
|  |  - Mini-Editor Inline com Atalhos Globais (Ctrl+Enter, Del, Ctrl+N)     |  |
|  +------------------------------------+------------------------------------+  |
|                                       |                                       |
|  +------------------------------------v------------------------------------+  |
|  |                     SUBSISTEMAS NATIVOS DO WINDOWS                      |  |
|  |  * System Tray Icon (H.NotifyIcon / TaskbarIcon)                        |  |
|  |  * Windows Toast Notifications (Microsoft.Toolkit.Uwp.Notifications)    |  |
|  |  * Windows DPAPI (ProtectedData.Protect com escopo do Usuário)          |  |
|  |  * Cronômetro Proativo Diário (DispatcherTimer)                         |  |
|  +------------------------------------+------------------------------------+  |
|                                       |                                       |
|  +------------------------------------v------------------------------------+  |
|  |                     MOTOR DO AGENTE LOCAL (UTTU CORE)                   |  |
|  |  * Orquestrador de Contexto e Memórias (SQLite nativo em uttu.db)       |  |
|  |  * Analisador de Tom e Construtor de Prompts Multi-Plataforma           |  |
|  |  * Renderizador SkiaSharp 2D (Cartões 1600x900 em %LocalAppData%\cache) |  |
|  +------------------------------------+------------------------------------+  |
|                                       | Stdio (JSON-RPC 2.0)                  |
|  +------------------------------------v------------------------------------+  |
|  |                     BUFFER MCP RUNNER (PROCESSO FILHO)                  |  |
|  |  * Executa Server/buffer-mcp.js ou mcp-buffer-server.exe                |  |
|  |  * Gerenciado por Windows Job Object (JOB_OBJECT_LIMIT_KILL_ON_CLOSE)  |  |
|  +-------------------------------------------------------------------------+  |
+---------------------------------------+---------------------------------------+
                                        | HTTPS Seguro
+---------------------------------------v---------------------------------------+
|                       BUFFER API (api.bufferapp.com)                          |
|             [X/Twitter]      [LinkedIn]      [Threads / Bluesky]              |
+-------------------------------------------------------------------------------+
```

---

## 🚀 Como Executar

### Pré-requisitos
- Windows 10 (Build 19041 ou superior)
- .NET 8.0 SDK

### Executar em Modo de Desenvolvimento
```powershell
dotnet run
```

### Executar a Suíte de Testes Automatizados (6/6 Subsistemas)
```powershell
dotnet run --project Tests/Uttu.Tests.csproj
```

### Publicar Executável Standalone Single-File (.exe)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```
O executável final estará disponível em:
`bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Uttu.exe`

---

## ⌨️ Atalhos de Teclado

| Atalho | Ação |
|---|---|
| `Ctrl + Enter` | Aprova o rascunho em foco e envia diretamente para a fila do Buffer |
| `Delete` | Descarta o rascunho |
| `Ctrl + N` | Foca no campo de criação de nova memória na Central de Contexto |

---

## 🎨 Especificação Flat Design (Windows 10)

- **Cores Estritas:**
  - Fundo da Janela: `#121212`
  - Cartões e Painéis: `#1E1E1E`
  - Superfícies de Entrada: `#252525`
  - Bordas: Linhas nítidas de 1px sólido `#2B2B2B`
  - Ação Primária: `#0078D7` (Azul Windows 10)
  - Botão Descartar: `#D83B01` (Vermelho Plano)
- **Tipografia:** `Segoe UI` (Interface), `Consolas` (Metadados e Contadores).
- **Arredondamento:** Ângulos retos (`CornerRadius = 0`).
- **Sombras:** Zero sombras projetadas ou efeitos acrílicos de alto consumo de GPU.
