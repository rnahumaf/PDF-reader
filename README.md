# PDF Reader Lite

Leitor de PDF ultra lightweight e minimalista para Windows, com foco total no conteudo do documento.

## Funcionalidades atuais (MVP)

- Abertura de PDF por botao `Abrir`
- Abertura por arrastar e soltar
- Abertura por argumento de linha de comando (duplo clique em `.pdf` via associacao)
- Navegacao de paginas (anterior/proxima, digitar pagina e miniaturas com selecao direta)
- Zoom auto-ajustavel (`Ajustar largura` e `Ajustar pagina`) e manual (`+`, `-`, percentuais)
- Impressao com dialogo nativo do Windows
- Modo `Formulario` para preencher campos de PDFs interativos
- PDFs com formulario sao detectados e abertos automaticamente em modo `Formulario`
- No modo `Formulario`, use a barra nativa do visualizador para navegar/imprimir/salvar
- Interface discreta, sem abas largas e sem distracoes

## Atalhos

- `Ctrl+O`: abrir arquivo
- `Ctrl+P`: imprimir
- `Ctrl+E`: alternar modo formulario (preenchimento de campos)
- `Ctrl++`: zoom in
- `Ctrl+-`: zoom out
- `Ctrl+0`: ajustar pagina
- `Ctrl+1`: ajustar largura
- `PgUp` / `PgDn`: pagina anterior/proxima

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

Observacao: `build-installer.ps1` executa o publish automaticamente antes de compilar o instalador.
Se voce ja tiver publicado e quiser pular essa etapa:

```powershell
.\scripts\build-installer.ps1 -SkipPublish
```

## Associacao como leitor de PDF no Windows

O instalador registra o app para abrir `.pdf` (opcao `associatepdf`).

Importante: no Windows 10/11, definir automaticamente como app padrao e limitado pelo sistema. Apos instalar, voce pode definir como padrao em:

- `Configuracoes > Aplicativos > Aplicativos padrao > Escolher padrao por tipo de arquivo > .pdf`

## Proximos passos sugeridos

- busca de texto
- modo tela cheia
- remember last page por arquivo
- performance incremental para PDFs muito longos
