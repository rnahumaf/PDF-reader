# PDF Reader Lite

Leitor de PDF ultra lightweight e minimalista para Windows, com foco total no conteudo do documento.

## Funcionalidades atuais (MVP)

- Abertura de PDF por atalho `Ctrl+O`
- Abertura por arrastar e soltar
- Abertura por argumento de linha de comando (duplo clique em `.pdf` via associacao)
- Visualizacao nativa do PDF via Edge/WebView2, com barra propria (navegacao, zoom, sumario, imprimir e fullscreen)
- Formulario interativo (AcroForm/XFA quando suportado pelo motor do Edge/WebView2)
- Impressao pelo proprio visualizador nativo
- Botao `Tela cheia` do app (F11), evitando o bug do fullscreen nativo do viewer
- Interface focada no documento, sem barra manual de navegacao

## Atalhos

- `Ctrl+O`: abrir arquivo
- `Ctrl+P`: imprimir
- `F11`: alternar tela cheia do app

Os atalhos de navegacao/zoom do documento continuam os nativos do visualizador PDF embutido.

## Desenvolvimento local

Pre-requisitos:

- .NET SDK 8.0+
- Microsoft Edge WebView2 Runtime (para o modo de formulario)

Comandos:

```powershell
dotnet build PdfReaderLite/PdfReaderLite.csproj
dotnet run --project PdfReaderLite/PdfReaderLite.csproj -- "C:\caminho\arquivo.pdf"
```

## Publicacao (exe para distribuicao)

```powershell
.\scripts\publish-win-x64.ps1
```

Saida: `publish/win-x64`

## Instalador Windows

Pre-requisito adicional:

- Inno Setup 6 (`ISCC.exe`)

Gerar instalador:

```powershell
.\scripts\build-installer.ps1
```

Saida: `dist/PDFReaderLite-Setup-<versao>.exe`

Observacoes:

- `build-installer.ps1` incrementa automaticamente a versao do app a cada execucao.
- O script executa publish antes de compilar o instalador para manter o executavel e o instalador com a mesma versao.

## Associacao como leitor de PDF no Windows

O instalador registra o app para abrir `.pdf` (opcao `associatepdf`).

Importante: no Windows 10/11, definir automaticamente como app padrao e limitado pelo sistema. Apos instalar, voce pode definir como padrao em:

- `Configuracoes > Aplicativos > Aplicativos padrao > Escolher padrao por tipo de arquivo > .pdf`

## Proximos passos sugeridos

- busca de texto
- modo tela cheia
- remember last page por arquivo
- performance incremental para PDFs muito longos
