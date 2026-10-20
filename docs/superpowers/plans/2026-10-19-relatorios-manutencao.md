# Relatórios de manutenção Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir uma ferramenta Windows distribuída pelo OneDrive que consulte diretamente o PCME, combine planilhas e gere HTMLs de manutenção por contrato e mês.

**Architecture:** Um executável inicia ASP.NET Core em endereço de loopback e abre uma interface Razor Pages no navegador. Um núcleo de cálculos independente das fontes recebe registros normalizados; adaptadores Firebird e planilhas alimentam snapshots individuais. Rascunhos e credenciais ficam locais ao computador, e os HTMLs emitidos são arquivos independentes.

**Tech Stack:** .NET 10 LTS / C#, ASP.NET Core Razor Pages, FirebirdSql.Data.FirebirdClient 10.3.4, ExcelDataReader 3.9.0, CsvHelper 33.1.0, System.Text.Json, proteção de credenciais com DPAPI Windows, xUnit e publicação self-contained win-x64. Não usar Entity Framework, Node ou servidor central nesta versão.

**Spec:** [Desenho aprovado na conversa](../specs/2026-10-19-relatorios-manutencao-design.md). Ler também `CONTEXT.md` e `docs/levantamento-relatorios.md`.

## Global Constraints

- “A interface não exige uso do DBeaver ou comandos SQL na rotina de geração.”
- “A janela prevista será das 5h às 17h, de segunda-feira a sábado, no horário local da operação.”
- “Uma parada física com duração total igual ou superior a quatro horas será elegível.”
- “As quatro horas são um filtro de inclusão, não uma franquia.”
- “Usar o tempo real em oficina dos atendimentos concluídos, incluindo espera por peças e atendimento durante a imobilização.”
- “Uma resposta ao formulário será uma solicitação, mesmo contendo vários problemas.”
- “FINALIZADO será suficiente mesmo sem DATA CONCLUSÃO preenchida.”
- “Não extrapolar distâncias para dias sem medição.”
- “Configuração de conexão e credenciais ficarão locais ao computador, fora da pasta compartilhada de distribuição.”
- “Um arquivo já emitido não será alterado por uma atualização dos dados.”
- Não gravar no PCME. Não considerar OS aberta como prova de imobilização. Não publicar dados operacionais, credenciais, exports ou caches no repositório público.
- SDK apenas na máquina de desenvolvimento; pacote final inclui runtime. Validar o alvo Windows x64 no segundo computador; não afirmar compatibilidade com outra arquitetura sem teste.

## Review Focus

1. Arquivo do OneDrive incompleto, bloqueado ou trocado durante leitura: preservar o último snapshot válido e mostrar a falha; teste na tarefa 4.
2. Formulário com cabeçalho de veículo vazio ou colunas reordenadas: exigir mapeamento explícito, nunca confundir veículo e hodômetro; teste na tarefa 4.
3. Duas instâncias locais salvando o mesmo rascunho: detectar revisão desatualizada e evitar perda silenciosa; teste na tarefa 6.
4. Transferência durante parada e atribuição ao contrato de origem sem cobertura suficiente: expor pendência em vez de limitar percentual; teste na tarefa 3.
5. Fonte passa a retornar erro ou muda seu formato: conservar a última leitura identificada e impedir aparência de atualização bem-sucedida; teste nas tarefas 1, 4 e 7.

## Estado inicial e preparação

A pasta contém documentação e SQLs de descoberta, sem aplicação ou checkout Git. O repositório `tropicaltransportes/relatorios` foi consultado pelo conector e tinha tamanho zero. O computador tem runtime .NET 10, mas não foi encontrado SDK em `dotnet --list-sdks`. O CLI gh não conseguiu alcançar a API neste ambiente; o conector GitHub funcionou. Não há conexão autenticada ao PCME disponível para os testes automatizados.

Na execução, preservar os arquivos existentes. Usar a skill de worktrees para avaliar isolamento; não presumir que já há repositório local. Preparar o SDK 10 pelo distribuidor oficial em diretório de ferramentas permitido, fixar em `global.json` a versão instalada e registrar o caminho usado nos comandos. Downloads, restauração de pacotes e escritas fora dos caminhos permitidos seguem as permissões do ambiente.

Nos comandos abaixo, `$dotnetExe` é o caminho absoluto do SDK validado. O comando básico de verificação é `& $dotnetExe test Relatorios.sln -c Release`. Cada tarefa usa filtros para o ciclo inicial e a suíte completa após mudanças que afetem dependências.

Versionar apenas código, documentação sanitizada e fixtures sintéticas. Antes de qualquer commit, excluir de controle de versão `.tools/`, `.local/`, `artifacts/`, `bin/`, `obj/`, exports operacionais e arquivos de conexão. `docs/levantamento-relatorios.md` contém exemplos operacionais reais e deve permanecer local até existir uma versão sanitizada; incluí-lo nas exclusões. Commits locais podem ser criados quando houver repositório disponível; push, PR e integração não fazem parte deste plano. Se a configuração de Git estiver impedida, preservar alterações e informar isso sem apagar a documentação nem simular commits.

## Arquivos e responsabilidades

| Caminho | Responsabilidade |
| --- | --- |
| Relatorios.sln, global.json, Directory.Build.props | Solução, SDK e compilação |
| src/Relatorios.Core/Domain/ | Tipos de domínio e validação dos registros |
| src/Relatorios.Core/Indicators/ | Disponibilidade, MTTR, MKBF e solicitações |
| src/Relatorios.Core/Reports/ | Agregação por contrato/mês e rastreabilidade |
| src/Relatorios.Infrastructure/Pcme/ | Conexão Firebird e normalização confirmada |
| src/Relatorios.Infrastructure/Imports/ | CSV/XLSX e publicação de solicitações |
| src/Relatorios.Infrastructure/Storage/ | Snapshots, cadastros e rascunhos individuais |
| src/Relatorios.Infrastructure/Export/ | HTML independente e nome de emissão |
| src/Relatorios.App/ | Inicialização local, Razor Pages e coordenação |
| tests/Relatorios.Core.Tests/ | Regras e cenários de domínio |
| tests/Relatorios.Infrastructure.Tests/ | Adaptadores, armazenamento e exportação |
| tests/Relatorios.App.Tests/ | Fluxo HTTP e isolamento entre contratos |
| tests/Fixtures/ | Dados sintéticos, sem registros reais dos motoristas |
| scripts/publish.ps1 | Publicação Windows reproduzível |
| docs/uso.md | Configuração e rotina do operador |

## Contratos de dados compartilhados

Definir em `src/Relatorios.Core/Domain/Models.cs` e dividir por conceito quando o arquivo deixar de ser legível. Namespace raiz `Relatorios`.

- `VehicleId(string Value)` e `ContractId(string Value)`: identificadores textuais; não converter IDs para número.
- `ReportMonth(int Year, int Month)`: limites do mês, calculados na zona America/Fortaleza; instantes normalizados como DateTimeOffset.
- `Assignment(string Id, VehicleId Vehicle, ContractId Contract, DateTimeOffset Start, DateTimeOffset? End, bool Confirmed)`: intervalo semiaberto de vínculo.
- `OfficeStay(string Id, string OccurrenceId, VehicleId Vehicle, string? WorkOrderId, DateTimeOffset Start, DateTimeOffset? End, MaintenanceNature Nature, ContractId? OriginContract, bool Confirmed)`: intervalo físico; `MaintenanceNature` distingue Failure, Planned e Unknown.
- `MileageReading(string SourceId, VehicleId Vehicle, DateTimeOffset At, decimal Value, string Unit)` e `MileageTotal(VehicleId Vehicle, DateTimeOffset Start, DateTimeOffset End, decimal Kilometers, bool Confirmed, string SourceId)`: leitura acumulada e distância confirmada são tipos diferentes.
- `MaintenanceRequest(string SourceId, VehicleId Vehicle, DateTimeOffset OpenedAt, string Status, string Description)`: cada envio; não armazenar e-mail ou nome quando dispensáveis à identificação.
- `PendingIssue(string Code, string Message, string? RecordId, VehicleId? Vehicle)`: pendência rastreável.
- `MetricStatus`: Available, Unavailable e NoOccurrences. `MetricValue(decimal? Value, MetricStatus Status, IReadOnlyList<PendingIssue> Issues)`; unidade definida pela propriedade do relatório.
- `ContractTargets(decimal AvailabilityMinimum, decimal MttrMaximumHours, decimal RequestsMinimum, decimal? MkbfMinimum)`: percentuais na escala 0–100; padrões 90, 18 e 70.
- `SourceState(string Name, DateTimeOffset? LastSuccessAt, string? LastError)` e `SourceSnapshot<T>(IReadOnlyList<T> Records, SourceState State, IReadOnlyList<PendingIssue> Issues)`.
- `ReportInput(ContractId Contract, ReportMonth Month, DateTimeOffset AsOf, IReadOnlyList<Assignment> Assignments, IReadOnlyList<OfficeStay> Stays, IReadOnlyList<MileageReading> Readings, IReadOnlyList<MileageTotal> ConfirmedMileage, IReadOnlyList<MaintenanceRequest> Requests, ContractTargets Targets, IReadOnlyList<SourceState> Sources)`.
- `ReportComments(string Summary, IReadOnlyDictionary<string,string> VehicleNotes, string NextActions)` e `ReportResult(ContractId Contract, ReportMonth Month, MetricValue AvailabilityPercent, MetricValue MttrHours, MetricValue MkbfKilometers, MetricValue RequestsPercent, IReadOnlyList<VehicleRankingRow> Ranking, IReadOnlyList<PendingIssue> Issues, IReadOnlyList<SourceState> Sources, ContractTargets Targets)`; definir `VehicleRankingRow(VehicleId Vehicle, decimal UnavailableHours, string? MainCause)`.
- `ReportDraft(string Id, ReportInput Input, ReportComments Comments, long Revision)`; persistir a referência às fontes sem credenciais.

## Tarefa 1: Diagnóstico e leitor do PCME sem escrita

**Files:** criar projetos da solução; `src/Relatorios.Infrastructure/Pcme/{ConnectionOptions,ConnectionProbe,PcmeReader}.cs`; `src/Relatorios.Core/Sources/PcmeModels.cs`; `tests/Relatorios.Infrastructure.Tests/PcmeReaderTests.cs`; `.gitignore` e arquivos de compilação. Core usa net10.0; projetos Windows de aplicação/infraestrutura e seus testes usam net10.0-windows. Core não referencia Infrastructure.

**Interfaces:** produzir `ConnectionProbe.ProbeAsync(ConnectionOptions, CancellationToken) -> Task<ConnectionProbeResult>` e `PcmeReader.ReadAsync(ConnectionOptions, ReportMonth, CancellationToken) -> Task<SourceSnapshot<PcmeWorkOrder>>`. `ConnectionOptions` contém Server, Port, Database, User, Password e Charset; Password não é serializado nem registrado. `PcmeWorkOrder` no Core contém Id, Vehicle, Type e dicionários OriginalDates, OriginalTimes e OriginalFlags com os nomes exatos do banco, além de Odometer; não infere intervalo físico. `PcmeVehicle` contém Vehicle, Plate, Description e leitura atual opcional. `ConnectionProbeResult` contém sucesso, versão do servidor e erro sanitizado.

- [ ] Escrever testes `PcmeReaderTests.TimeoutDoesNotExposeConnectionSecrets`, `UsesReadOnlyTransaction` e `PeriodFilterIncludesOrdersOpenedBeforeMonthAndNotYetReleased`; executar `& $dotnetExe test --filter FullyQualifiedName~PcmeReaderTests` e observar a falha relevante antes de implementar.
- [ ] Preparar o SDK, projetos e referências; implementar o leitor com FirebirdSql.Data.FirebirdClient. Obter versão por consulta de metadados do servidor e usar SQL parametrizado compatível com a versão detectada; manter a sintaxe simples sem CTE na descoberta inicial. Consultar colunas explícitas, com timeout e cancelamento. Os únicos comandos permitidos contra o PCME são SELECT em transação de leitura.
- [ ] Incluir SITUACAO_EQP, TIPO_SERVICO, PRE_LIBERACAO e campos temporais na leitura bruta; consultar veículos e leituras em métodos do mesmo adaptador, retornando `SourceSnapshot<PcmeVehicle>` e `SourceSnapshot<MileageReading>`. Não usar a amostra FIRST 60 como consulta de produção.
- [ ] Rodar os testes filtrados e confirmar que credenciais e connection string não aparecem em erros. Provar comportamento de timeout também em falha de conexão; testes com executor substituível não comprovam conexão real.
- [ ] Registrar separadamente a verificação ao vivo para quando o operador preencher a configuração local: versão, leitura de OS conhecida e ausência de escrita. Ausência de credencial não impede construir os outros componentes, mas impede declarar integração validada. Commit: `feat: add read-only PCME reader` quando Git estiver disponível.

## Tarefa 2: Modelo físico e normalização sem inferências de parada

**Files:** criar `src/Relatorios.Core/Domain/{Models,OfficeStayNormalizer,VehicleMatcher}.cs`; `src/Relatorios.Core/Sources/OfficeMappingProfile.cs`; `tests/Relatorios.Core.Tests/OfficeStayNormalizerTests.cs`.

**Interfaces:** consumir `PcmeWorkOrder`; produzir `OfficeStayNormalizer.Normalize(IReadOnlyList<PcmeWorkOrder>, OfficeMappingProfile, IReadOnlyList<OfficeStay> confirmedOverrides) -> SourceSnapshot<OfficeStay>` e `VehicleMatcher.Resolve(string sourceName, string rawId, IReadOnlyDictionary<string,VehicleId> aliases) -> VehicleMatchResult`. O perfil identifica campos de entrada/liberação e critério de imobilização, acompanhado de `Confirmed` e evidência descritiva sem credenciais. `VehicleMatchResult` contém ID ou pendência.

- [ ] Escrever `OpenAdministrativeOrderDoesNotCreateContinuousStay` com uma OS aberta em 07/10 e operação posterior em 19/10: perfil não confirmado deve retornar zero intervalos automáticos e pendência. Escrever `WorkingGapIsNotAddedToPhysicalDuration`, `UnknownVehicleAliasRequiresReview` e `PreservesLeadingZeroAndSuffix`; executar o filtro OfficeStayNormalizerTests e confirmar falha.
- [ ] Implementar intervalos semiabertos e validações de fim anterior ao início, campos ausentes, múltiplas visitas e natureza desconhecida. Uma confirmação manual tem prioridade identificada e não modifica o PCME.
- [ ] Tratar CO/SO como sugestão de falha, IN/PR como sugestão de programada e PD como ambígua; não converter sugestão em classificação confirmada para cálculo. A interface da tarefa 7 permitirá confirmar a ocorrência, não cada item de serviço.
- [ ] Validar o perfil do PCME comparando registros reais disponíveis com entrada/liberação conhecidas, sem exigir captura de tela. Enquanto essa validação não existir, retornar pendências e permitir confirmações manuais; nunca adotar abertura/encerramento administrativo como substituto silencioso.
- [ ] Executar os testes filtrados, verificar precedência dos complementos e preservar lacunas entre visitas. Commit: `feat: normalize confirmed workshop stays`.

## Tarefa 3: Disponibilidade e MTTR verificáveis

**Files:** criar `src/Relatorios.Core/Indicators/{OperatingCalendar,AvailabilityCalculator,MttrCalculator}.cs`; `tests/Relatorios.Core.Tests/{AvailabilityCalculatorTests,MttrCalculatorTests}.cs`.

**Interfaces:** produzir `OperatingCalendar.Intersect(DateTimeOffset start, DateTimeOffset end) -> IReadOnlyList<TimeRange>` com `TimeRange(Start,End)`; `AvailabilityCalculator.Calculate(ReportInput) -> MetricValue` e `MttrCalculator.Calculate(ReportInput) -> MetricValue`. Datas são convertidas explicitamente à zona da operação; não depender do fuso configurado no Windows.

- [ ] Escrever testes com resultados exatos: segunda-feira 5h–11h desconta 6h; 5h–9h desconta 4h; 5h–8h59 não entra; 18h–8h do dia seguinte desconta apenas 5h–8h quando aplicável; domingo desconta zero. Duas OS 5h–11h e 8h–14h do mesmo veículo descontam 9h, não 12h.
- [ ] Acrescentar `CrossMonthSplitsUnavailableHours`, `MidMonthAssignmentChangesExpectedHours`, `TransferWithoutOriginResponsibilityCoverageReturnsPending`, `MissingSourceDoesNotLookLikeFullAvailability` e `ZeroExpectedHoursIsUnavailable`; executar os filtros AvailabilityCalculatorTests e MttrCalculatorTests e observar falhas.
- [ ] Implementar elegibilidade pela duração física total >= 4h, união de intervalos por veículo/contrato, interseção com calendário e denominador por vínculo confirmado. Não limitar números inválidos a 0–100; devolver pendência de integridade.
- [ ] Para MTTR: testes de atendimentos concluídos de 2h e 6h devem resultar 4h; atendimento aberto não entra; OS administrativa de 48h com 6h físicas resulta 6h. Agrupar por OccurrenceId sem duplicar intervalos, manter a competência de início e incluir esperas físicas fora da janela operacional.
- [ ] Rodar os testes filtrados e a suíte Core. Commit: `feat: calculate physical availability and MTTR`.

## Tarefa 4: Importação das planilhas e publicação de pedidos

**Files:** criar `src/Relatorios.Infrastructure/Imports/{TabularReader,ColumnMapping,OperationImporter,RequestImporter,PublishedRequestsReader}.cs`; `tests/Relatorios.Infrastructure.Tests/{OperationImporterTests,RequestImporterTests,PublishedRequestsReaderTests}.cs`; fixtures sintéticas CSV/XLSX.

**Interfaces:** `TabularReader.Read(Stream input, string extension) -> TabularData` preserva células textuais e datas tipadas; `OperationImporter.Import(TabularData, ColumnMapping) -> SourceSnapshot<OperationRecord>`; `OperationRecord` contém SourceId, Vehicle, At, ContractLabel e Odometer. `RequestImporter.Import(TabularData, ColumnMapping) -> SourceSnapshot<MaintenanceRequest>`; `PublishedRequestsReader.ReadAsync(Uri publication, ColumnMapping, CancellationToken) -> Task<SourceSnapshot<MaintenanceRequest>>`.

- [ ] Testar IDs 01/150V, datas pt-BR, acentos, descrições com vírgulas/quebras de linha e hodômetro com zero inicial; não somar leituras. `MissingVehicleHeaderRequiresExplicitMapping` não deve adivinhar pela posição. Colunas reordenadas devem funcionar após mapeamento salvo. Executar os filtros dos três importadores e observar falhas.
- [ ] Implementar leitura streaming XLSX com ExcelDataReader e CSV com CsvHelper. Criar cópia de leitura consistente em diretório temporário permitido; falhas de lock, arquivo incompleto ou troca durante leitura não substituem o snapshot anterior.
- [ ] Ler a aba publicada gid 1115240100 pelo endpoint CSV derivado da publicação fornecida; validar resposta e cabeçalhos antes de importar. HTML de erro, autenticação ou resumo Página1 deve produzir falha específica, sem ser aceito como dados. Permitir arquivo CSV/XLSX como alternativa pela mesma interface de normalização.
- [ ] Testar `RefreshPreservesRequestIdentityWhenStatusOrRowOrderChanges`, `SeveralProblemsRemainOneRequest` e `SimilarResubmissionsRemainDistinctAndFlagged`. Identidade canônica combina origem da publicação, carimbo e veículo, sem usar a posição da linha nem situação; colisões no mesmo carimbo/veículo permanecem pendentes de identificação, sem juntar pedidos automaticamente. Preservar uma revisão do conteúdo para alterações de descrição não criarem novos pedidos.
- [ ] Testar `NetworkFailurePreservesDatedSnapshot` e `UnexpectedStatusProducesIssue`; situação desconhecida não vira FINALIZADO. Confirmar leitura real da publicação quando a rede permitir; registrar se foi usado arquivo alternativo. Commit: `feat: import operation and driver requests`.

## Tarefa 5: MKBF, solicitações e composição do relatório

**Files:** criar `src/Relatorios.Core/Indicators/{MileageValidator,MkbfCalculator,RequestCalculator}.cs`; `src/Relatorios.Core/Reports/ReportBuilder.cs`; `tests/Relatorios.Core.Tests/{MkbfCalculatorTests,RequestCalculatorTests,ReportBuilderTests}.cs`.

**Interfaces:** `MileageValidator.Validate(ReportInput) -> MileageCoverage` com distâncias, população/período cobertos e pendências; `MkbfCalculator.Calculate(ReportInput) -> MetricValue`; `RequestCalculator.Calculate(ReportInput) -> MetricValue`; `ReportBuilder.Build(ReportInput) -> ReportResult`. Usar os calculadores da tarefa 3, sem duplicar fórmulas na interface.

- [ ] Escrever testes: 10.000 km confirmados e duas falhas elegíveis => MKBF 5.000 km; preventiva de 8h não conta como falha; corretiva física de 4h conta; natureza desconhecida exige conferência; mesmo OccurrenceId em dois itens conta uma vez. Sem quilômetros válidos => indisponível; quilômetros válidos e zero falhas => NoOccurrences. Executar o filtro MkbfCalculatorTests e observar falhas.
- [ ] Implementar distância com fronteiras verificadas ou total manual confirmado. Leituras dentro do mês sem cobrir suas fronteiras não comprovam distância mensal. Queda de hodômetro gera pendência; saltos sem confirmação e unidade diferente de km não entram automaticamente. Não extrapolar nem exigir dia de viagem onde o veículo não estava alocado; avaliar cobertura do intervalo confirmado.
- [ ] Testar `FinalizedWithoutCompletionDateCountsInOpeningMonth`: dois pedidos em junho, um FINALIZADO em julho => junho 50%. Status vazio fica no denominador se veículo/período/contrato forem resolvidos, acompanhado de pendência. Nenhum pedido => NoOccurrences. Atualização da situação não altera SourceId.
- [ ] Construir consolidado somente com população/período consistente; cobertura incompleta mantém MKBF consolidado indisponível, expondo resultados válidos por veículo. Calcular ranking de indisponibilidade pela mesma base da disponibilidade e avaliar metas 90/18/70 apenas para resultados Available.
- [ ] `ReportBuilderTests.IsolatesContractData` deve garantir que o relatório A não inclua pedidos, comentários ou veículos exclusivos de B. Executar toda a suíte Core. Commit: `feat: compose contract maintenance indicators`.

## Tarefa 6: Persistência individual e configuração local

**Files:** criar `src/Relatorios.Infrastructure/Storage/{AppDataPaths,JsonStore,DraftStore,SettingsStore,SnapshotStore,WindowsCredentialStore}.cs`; `tests/Relatorios.Infrastructure.Tests/{DraftStoreTests,SnapshotStoreTests,CredentialStoreTests}.cs`.

**Interfaces:** `DraftStore.LoadAsync(ContractId, ReportMonth, CancellationToken) -> Task<ReportDraft?>`; `SaveAsync(ReportDraft draft, long expectedRevision, CancellationToken) -> Task<SaveDraftResult>`; `SettingsStore` preserva vínculos, aliases, perfis, metas e mapeamentos. `SnapshotStore` grava versões por fonte; `WindowsCredentialStore` guarda/recupera segredo protegido pelo usuário Windows. Definir SaveDraftResult como sucesso com revisão nova ou conflito explícito.

- [ ] Testar dois perfis A/B em diretórios `.local/test-profile-A` e B do workspace: salvar A não altera B; atualizar pacote não toca dados. `StaleRevisionDoesNotOverwriteDraft` simula duas instâncias. `FailedWritePreservesPreviousDraft` simula interrupção antes do rename. Executar filtros e observar falhas.
- [ ] Implementar JSON com escrita temporária e substituição atômica no mesmo volume, versão de esquema e leitura que rejeite formato desconhecido sem apagá-lo. Guardar dados operacionais em `%LOCALAPPDATA%/Tropical/Relatorios`, fora do OneDrive. Permitir override de raiz apenas para testes.
- [ ] Separar segredos da configuração exportável e proteger com DPAPI CurrentUser; não aproveitar ou descriptografar o cofre de senhas do DBeaver. Interface local recebe os parâmetros autorizados de conexão e oferece teste, com erros sanitizados.
- [ ] Testar que nenhum segredo aparece em JSON comum, logs ou dados do relatório. Rascunhos preservam comentários após atualização das fontes, associados a IDs estáveis; nunca gravar os dados locais em Git.
- [ ] Rodar testes filtrados e Infrastructure. Commit: `feat: persist individual drafts and settings`.

## Tarefa 7: Fluxo guiado no navegador local

**Files:** criar `src/Relatorios.App/Program.cs`, `Application/ReportService.cs`, `Pages/Settings/{Index,Mapping}.cshtml[.cs]`, `Pages/Reports/{Index,Review,Comments,Preview}.cshtml[.cs]`, `Pages/Shared/_Layout.cshtml`, `wwwroot/site.css`; `tests/Relatorios.App.Tests/ReportWorkflowTests.cs`.

**Interfaces:** `ReportService.RefreshAsync(ContractId, ReportMonth, CancellationToken) -> Task<ReportResult>` coordena leitores, snapshots e ReportBuilder; `ReportService.LoadDraftAsync`/`SaveDraftAsync` delegam ao DraftStore; páginas usam modelos de apresentação e não executam SQL ou refazem fórmulas.

- [ ] Escrever teste de integração HTTP que configure fontes sintéticas, selecione contrato/mês, obtenha pendências, salve três campos de comentário e recarregue o rascunho. A descrição do problema pode conter `<script>` e deve aparecer como texto. Executar ReportWorkflowTests e observar falha.
- [ ] Implementar quatro etapas reconhecíveis: Selecionar; Conferir; Comentar; Gerar. Campos de configuração ficam na tela de configuração, sem exigir SQL do operador. Mostrar data da última atualização de cada fonte, erro de atualização e motivos dos resultados indisponíveis.
- [ ] Implementar cadastros de contratos, vínculos e aliases; confirmações de intervalos físicos, natureza da ocorrência, quilômetros e possíveis reenvios ficam nas pendências relacionadas. Metas configuráveis com validação de escala e intervalos; valores iniciais 90%, 18h e 70%.
- [ ] Iniciar Kestrel apenas em loopback com porta livre, abrir navegador com ProcessStartInfo/UseShellExecute e oferecer saída para encerrar o processo. Usar antiforgery das páginas, validação de Host/Origin e nenhuma política CORS ampla. Não abrir acesso da rede ao processo local.
- [ ] Testar `SourceFailureShowsPreviousSnapshotTimestamp`, `DraftConflictIsVisible`, `TwoContractsKeepIndependentComments` e seleção de vários contratos com revisões individuais. Fazer inspeção visual do fluxo em desktop e largura reduzida; mensagens devem ser em português e indicar a ação útil. Commit: `feat: add guided report workflow`.

## Tarefa 8: Emissão de HTML independente

**Files:** criar `src/Relatorios.Infrastructure/Export/{HtmlReportRenderer,ReportFileName}.cs`; `src/Relatorios.App/Pages/Reports/Export.cshtml.cs`; `tests/Relatorios.Infrastructure.Tests/HtmlReportRendererTests.cs`.

**Interfaces:** `HtmlReportRenderer.Render(ReportResult, ReportComments, string author, DateTimeOffset emittedAt) -> string`; `ReportFileName.Create(ContractId, ReportMonth, DateTimeOffset, Guid emissionId) -> string`. A emissão é criada a partir do resultado revisado, sem reler o banco silenciosamente durante a exportação.

- [ ] Escrever `GeneratedHtmlContainsNoExternalResources`, `EscapesSourceAndCommentText`, `DoesNotContainOtherContractOrDriverEmail`, `MissingMetricExplainsReason`, `FilenamesDoNotCollideAcrossUsers` e `OlderEmissionRemainsUnchangedAfterRefresh`; executar o filtro e observar falhas.
- [ ] Implementar HTML com CSS incorporado, indicadores e metas, ranking, comentários e impressão. Gráficos simples podem usar SVG incorporado derivado apenas dos dados de apresentação; conteúdo de texto é codificado por HtmlEncoder. Sem dependências externas ou conexão ao backend no arquivo emitido.
- [ ] Permitir prévia e geração de vários arquivos, com nome legível e Guid curto. Tratar nomes de contrato com caracteres inválidos no Windows e nomes reservados; não substituir arquivo existente. Se houver informação material ausente, identificar relatório parcial e motivos.
- [ ] Abrir um HTML gerado com a aplicação encerrada e acesso de rede indisponível; verificar visualmente todos os blocos e impressão. Confirmar que o texto e as unidades permanecem legíveis. Commit: `feat: export standalone maintenance HTML reports`.

## Tarefa 9: Pacote Windows e aceitação operacional

**Files:** criar `scripts/publish.ps1`, `docs/uso.md`, `docs/validacao-operacional.md`; ajustar arquivos csproj para publicação self-contained e recursos Razor/static incorporados conforme necessário.

**Interfaces:** script produz `artifacts/Relatorios-win-x64/` com executável e todos os recursos necessários, sem fontes operacionais, configuração local, rascunhos ou credenciais. Não mover versões de terceiros nem apagar a pasta compartilhada do usuário.

- [ ] Testar publicação em diretório novo e verificar arquivos esperados e ausência de segredos; repetir com execução a partir de caminho contendo espaços e acentos. Comando: `& $dotnetExe publish src/Relatorios.App/Relatorios.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/Relatorios-win-x64`. Preferir pasta autossuficiente à promessa de um único arquivo quando recursos necessários exigirem arquivos adjacentes.
- [ ] Executar `& $dotnetExe test Relatorios.sln -c Release`, verificar saída sem falhas e publicar. Iniciar o pacote pelo caminho de distribuição com perfil de teste, completar um relatório e confirmar persistência após reinício.
- [ ] Documentar configuração local de servidor/porta/base/usuário, teste de conexão, importações, revisão de pendências, comentários, emissão e atualização do pacote. Não incluir credenciais reais no guia. Registrar limitações efetivas e como usar a última carga identificada.
- [ ] Reconciliar casos conhecidos do PCME: veículo trabalhando com OS aberta, parada real de pelo menos quatro horas e atendimento liberado. Confirmar perfil de campos e classificação sem impor captura de tela. Comparar uma solicitação conhecida na planilha e os números do relatório de julho, explicando diferenças de regras e cobertura.
- [ ] Validar com o usuário o pacote em um segundo computador Windows x64 da mesma rede: inicia sem SDK, testa conexão ao PCME, usa seus próprios rascunhos e abre o HTML emitido por outro computador. O agente não deve alegar esse teste concluído sem evidência recebida ou acesso ao computador.
- [ ] Registrar em `docs/validacao-operacional.md` cada evidência, data, configuração não secreta e qualquer impedimento. Conexão ao vivo, fonte publicada ou segundo computador indisponíveis são itens pendentes de aceitação, não testes substituídos por mocks. Commit: `build: package Windows maintenance reporting tool`.

## Referências técnicas verificadas

- [.NET 10 LTS e ciclo de suporte](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- [Distribuição .NET self-contained e single-file](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
- [Provider .NET oficial do Firebird](https://www.firebirdsql.org/en/devel-net-provider/) e [pacote 10.3.4](https://www.nuget.org/packages/FirebirdSql.Data.FirebirdClient/10.3.4).
- [ExcelDataReader 3.9.0](https://www.nuget.org/packages/ExcelDataReader/3.9.0) e [CsvHelper 33.1.0](https://www.nuget.org/packages/CsvHelper/33.1.0).

## Revisão do plano

Cobertura da especificação: integração direta e configuração na tarefa 1; intervalos e IDs na 2; disponibilidade/MTTR na 3; planilhas e atualização na 4; MKBF/solicitações/ranking/metas na 5; rascunhos e complementos na 6; fluxo e revisão na 7; HTML na 8; distribuição e reconciliação na 9. Os cinco itens de Review Focus possuem testes atribuídos.

Não foi instalado SDK, criado projeto de aplicação, baixado pacote nem executada alteração no PCME durante o planejamento. O próximo passo é a revisão deste documento e a escolha do método de execução.