# Executar o workspace local

O workspace usa o perfil Windows atual e guarda os dados em `%LOCALAPPDATA%\Tropical\Relatorios`. Isso mantém rascunhos e configuração fora da pasta distribuída pelo OneDrive. A senha só é salva depois de uma conexão bem-sucedida e fica protegida por DPAPI para esse mesmo usuário Windows.

Para executar no computador de desenvolvimento:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
$env:DOTNET_ROOT = Join-Path (Get-Location) '.dotnet'
.\.dotnet\dotnet.exe run --project src/Relatorios.App/Relatorios.App.csproj
```

A ferramenta abre a interface no navegador e escuta apenas em `127.0.0.1`. Salve o contrato e a competência para criar ou retomar o rascunho local dessa combinação; fontes carregadas em outro contrato ou mês não aparecem no rascunho selecionado. Depois configure o servidor Firebird, a porta, o alias/caminho da base, o usuário, a senha e o charset. Use **Testar e salvar configuração protegida** antes de atualizar o PCME.

O campo **Resumo e observações** é salvo junto ao rascunho do contrato e competência selecionados. As fontes mostram a data do último carregamento bem-sucedido, a quantidade de registros preservada, as pendências daquela leitura e erros de atualização. Uma falha não apaga o snapshot válido anterior. Resultados de uma atualização que terminou depois de você mudar de contrato ou competência não são gravados na seleção nova.

Uma falha de atualização aparece como erro sanitizado e mantém os registros e a data do último sucesso. Registros sem identificador, veículo, data ou hodômetro válido tornam a leitura malsucedida e preservam o carregamento anterior. Pendências operacionais dos snapshots aparecem junto à fonte. As consultas atuais cobrem OS, veículos e leituras do PCME. A planilha de operação e as solicitações ainda não estão ligadas nesta etapa.

O pacote Windows sem SDK e a validação em segundo computador pertencem à etapa de distribuição posterior.