# Levantamento dos relatórios de manutenção

Registro da descoberta em andamento em 19/10/2026. Este documento reúne decisões confirmadas, evidências e questões abertas; ainda não é uma especificação aprovada para implementação.

## Objetivo e fluxo confirmados

Reduzir as consultas manuais ao PCME e os cruzamentos em planilhas para produzir relatórios separados por contrato, associado a um cliente.

O usuário poderá preparar relatórios quando precisar: selecionar contrato e período, carregar os dados, conferir indicadores e pendências, comentar e gerar um arquivo HTML independente.

Os comentários terão espaços para resumo do mês, ocorrências por veículo e próximas ações. Os rascunhos serão preservados para continuidade do trabalho.

A ferramenta será distribuída por uma pasta do OneDrive e utilizada por pessoas em computadores da mesma rede. Não é necessária edição compartilhada de um rascunho. Acesso ao banco em cada computador ainda será verificado. Não foi escolhida a tecnologia de implementação.

## Regras confirmadas

- Janela prevista de disponibilidade: 5h às 17h, de segunda-feira a sábado.
- Paradas com duração igual ou superior a quatro horas entram na disponibilidade e no MKBF. O limite não é uma franquia: uma parada de seis horas não vira duas horas.
- Para disponibilidade, considerar a interseção da parada com a janela prevista e distribuir as horas pelos meses em que ocorreram.
- Uma parada iniciada em um contrato permanece atribuída a esse contrato, mesmo que haja mudança antes da liberação.
- OS abertas afetam a disponibilidade com o tempo acumulado até a data da consulta, após atingir quatro horas.
- MTTR usa o tempo integral das OS concluídas, incluindo esperas.
- Solicitação atendida significa concluída. Uma solicitação aberta em junho e concluída em julho atualiza o percentual de junho.
- Consultas históricas podem atualizar indicadores; um HTML já emitido preserva o conteúdo daquela emissão.
- O hodômetro de entrada da OS é uma leitura acumulada, não a distância percorrida no mês.
- Quilometragem ausente não deve virar zero nem ser estimada silenciosamente. Calcular os resultados sustentados pelos dados e informar cobertura. O critério mínimo de cobertura para o MKBF consolidado ainda precisa ser definido.
- Histórico de contratos foi descrito como fonte verbal. O cadastro manual e as pendências de confirmação foram aceitos.

## Fontes examinadas

### Relatório de referência

Documento Word de Timbaúba, julho/2026: disponibilidade física, MTTR, MKBF, percentual de solicitações atendidas, ranking de veículos com maior indisponibilidade e análise escrita. O documento mostra resultados, mas não detalha as fórmulas de origem.

### Planilha de operação

Arquivo `INDICADORES - OCUPAÇÃO (v1).xlsx`, lido sem alterações. A aba OCUPAÇÃO contém 28.690 registros não vazios, de 02/01/2025 a 19/10/2026, com data, horário, contrato, veículo e hodômetro. Os registros abrangem cinco nomes de contratos e 83 identificadores de veículo; 32 identificadores aparecem em mais de um contrato.

Não foram encontrados campos vazios de data, contrato, veículo ou hodômetro nesses registros. Isso não comprova cobertura de todos os dias ou viagens nem correção das leituras. Registros de operação constituem evidência do contrato naquela ocorrência, sem provar vínculo contínuo em dias sem apontamento.

### Esquema do banco

CSV exportado pelo usuário: `_SELECT_TRIM_r_RDB_RELATION_NAME_AS_TABELA_CASE_WHEN_r_RDB_VIEW__202610191115.csv`, originalmente em Downloads. Contém 363 tabelas/views e 3.910 campos. A conexão local do DBeaver utiliza o driver Firebird/Jaybird; a versão do servidor ainda não foi confirmada.

| Finalidade | Tabela candidata | Evidência e limite |
| --- | --- | --- |
| Veículos | EQUIPAMENTOS | EQUIPAMENTO, PLACA, LOTACAO e leituras; identificadores precisam ser comparados à planilha |
| OS | ORDENS_DE_SERVICOS | OS, EQUIPAMENTO, DATA, HORA_INI, DATA_FIM, HORA_FIM, DATA_ENCERRA, ENCERRADA e HORIMETRO; significado das datas e flags ainda não validado |
| Solicitações | SERVICOS_PENDENTES | SOLICITACAO, DATA, FECHAMENTO, EQUIPAMENTO e OS; uma linha pode ser item, não solicitação inteira |
| Lotação atual | LOTACAO e EQUIPAMENTOS.LOTACAO | Códigos e descrições; equivalência entre lotação e contrato ainda não confirmada |
| Histórico de lotação | MOVIMENTO_LOTACAO | EQUIPAMENTO, DATA, DATA_SAIDA, LOTACAO e ODOMETRO; preenchimento e significado operacional ainda não confirmados |
| Leituras adicionais | HORIMETRO | EQUIPAMENTO, DATA, HORA, HORIMETRO e FLAG; unidade, qualidade e cobertura ainda não confirmadas |
| Classificação de OS | TIPOOS | TIPO e DESCRICAO; valores ainda não examinados |

Nomes de colunas sugerem relações, mas o CSV não contém PK/FK nem registros para comprová-las. A existência de MOVIMENTO_LOTACAO não prova que o histórico de contratos esteja registrado: pode representar outro conceito ou não ser preenchida.

## Próximas validações

1. Inspecionar amostras com `docs/consultas/amostras-pcme.sql` e comparar registros conhecidos no PCME.
2. Distinguir liberação física, fim do serviço e encerramento administrativo da OS, incluindo reaberturas e datas temporárias.
3. Confirmar o grão e o critério de conclusão das solicitações, evitando contar itens como solicitações.
4. Confirmar se lotação representa contrato e se seu histórico pode complementar as informações verbais.
5. Verificar identificadores, quilômetros inconsistentes e cobertura temporal antes de fechar a fórmula operacional do MKBF.
6. Definir critérios para consolidado com dados parciais, sobreposição de OS, metas e identificação da frota prevista.
7. Validar o acesso de consulta ao banco e o funcionamento da ferramenta num segundo computador da rede.

## Ocorrência na consulta de amostras

A primeira versão, com sete CTEs e UNION ALL, retornou no ambiente do usuário o erro Firebird 336397237/-104: CTE AMOSTRA_3 não utilizada. No arquivo completo, todas as CTEs estavam referenciadas; não foi confirmado se houve execução parcial ou comportamento específico da versão do servidor. A ordenação da união por nomes também não atendia à sintaxe documentada do Firebird 2.5.

A consulta foi reescrita com subconsultas independentes e ordenação por posições, preservando campos, origens e limite de amostragem. A verificação local foi estrutural. A tentativa de executar um banco de teste isolado foi impedida por uma falha de carregamento de DLL ao iniciar o isql instalado; não houve conexão ao PCME. A execução no DBeaver do usuário continua necessária para confirmar o resultado.

## Evidências da exportação de amostras

Foi encontrado em Downloads o arquivo relacionado `_SELECT_CAST_ORDENS_DE_SERVICOS_AS_VARCHAR_40_AS_ORIGEM_CAST_OS__202610191124.csv`, mais recente que o anexo de esquema reenviado. Ele contém as 23 colunas esperadas e 304 registros: 60 EQUIPAMENTOS, 60 HORIMETRO, 60 MOVIMENTO_LOTACAO, 60 ORDENS_DE_SERVICOS, 59 SERVICOS_PENDENTES e 5 TIPOOS. O resultado confirma execução de uma consulta de amostras; não houve linhas da origem LOTACAO.

- Das 60 OS amostradas, 12 têm encerramento S, data final e hora final. As outras 48 não têm encerramento nem data final. Em 34 dessas 48 há apontamento posterior de operação para o mesmo identificador na planilha de ocupação. Isso exige validar se OS aberta corresponde a imobilização ou pode representar programação/pendência administrativa, além de conferir a confiabilidade dos registros de operação.
- Exemplo para conferência: OS 51456346, veículo 150V, tipo IN, abertura em 07/10/2026 às 17h58, sem data final no CSV; a planilha registra operação desse veículo em 19/10/2026 às 7h16min31s, com leitura 101.799, superior à leitura 99.752 da OS. O tempo até a data de consulta não deve ser adotado automaticamente como parada contínua sem resolver essa divergência.
- Os 12 registros encerrados têm DATA_FIM e DATA_ENCERRA no mesmo dia. Essa coincidência na amostra não comprova que os campos tenham o mesmo significado. Os campos temporários de início e fim estão vazios nas 60 OS.
- SERVICOS_PENDENTES não mostrou identificadores SOLICITACAO preenchidos. São 59 linhas, com início mais recente em 05/01/2026; 42 linhas referenciam apenas 35 OS distintas. Ainda não é fonte validada para o indicador de solicitações e contar linhas pode duplicar eventos.
- TIPOOS contém CO/corretiva, IN/inspeção, PD/serviço pendente, PR/preventiva e SO/socorro. Existem sete OS recentes do tipo PD. Precisamos distinguir pedido, programação, falha e parada real antes de escolher inclusões por tipo.
- MOVIMENTO_LOTACAO apresenta datas até 24/07/2026, com DATA_SAIDA sempre vazia na amostra. Existem repetições de códigos, e não apenas mudanças. LOTACAO não retornou descrições; os 60 equipamentos amostrados também não têm LOTACAO preenchida. O histórico ainda não sustenta atribuição automática aos contratos.
- HORIMETRO contém leituras recentes com data e hora. É uma possível fonte complementar de quilômetros; cobertura de períodos completos e coerência das leituras ainda precisam ser avaliadas.

A regra discutida de descontar OS abertas após quatro horas fica pendente de revisão diante dos apontamentos de operação. A distinção entre pendência administrativa e indisponibilidade física deve ser resolvida antes da especificação dos cálculos.

## Esclarecimentos após análise das amostras

O usuário confirmou que o veículo pode trabalhar enquanto a OS permanece aberta. Portanto, fica substituída a regra inicial de usar automaticamente o tempo acumulado de toda OS aberta como indisponibilidade. Ainda precisamos identificar como reconhecer e medir uma imobilização real; OS abertas não devem ser descartadas se houver evidência de parada física.

Solicitações são pedidos de manutenção feitos pelos motoristas. A equivalência desses pedidos com SERVICOS_PENDENTES, OS do tipo PD ou outro registro ainda não foi confirmada.

O usuário aceitou distinguir as ocorrências: qualquer parada real elegível pode afetar disponibilidade, mas apenas falhas elegíveis compõem o denominador do MKBF. Preventivas e inspeções programadas não são automaticamente falhas. Os códigos de tipo da OS, sozinhos, ainda não foram validados como classificação suficiente.

O usuário informou que entrada e liberação são registradas no PCME e que os pedidos dos motoristas chegam em uma planilha. Essa planilha de solicitações é uma fonte adicional à planilha de ocupação já examinada. Ainda precisamos receber uma amostra, verificar o critério registrado de conclusão e identificar no PCME os campos que representam o intervalo físico de oficina.

## Planilha publicada de solicitações

O usuário forneceu a publicação Google Sheets [SOLICITAÇÃO DE SERVIÇOS](https://docs.google.com/spreadsheets/u/1/d/e/2PACX-1vR9Tej9PTimDa3-1cLuXwRxj8u4Sr_lKaJMPVmL4FlfJ3STTk0qdfjCRLD_uGu6dt9qJIuWtykzaz1b/pubhtml) e dispensou a captura de tela do PCME. Não solicitar novamente a captura como pré-requisito; a validação dos campos deve usar os dados disponíveis e comparações operacionais quando necessárias.

A publicação foi aberta em modo de leitura no navegador. Abas visíveis: Respostas ao formulário 1 (gid 1115240100) e Página1 (gid 1369572126). A primeira apresenta carimbo de data/hora, nome do solicitante, identificação do veículo, hodômetro, descrição dos problemas, e-mail, situação e campos de acompanhamento como data programada, data conclusão, veículo liberado, técnico e observações. O cabeçalho da identificação do veículo não apareceu na leitura de acessibilidade, embora os valores estejam presentes entre solicitante e hodômetro; verificar posições e cabeçalhos ao construir o importador.

Na parte inspecionada da aba de respostas há situações PENDENTE, EM ANDAMENTO e FINALIZADO, além de situação vazia. Há exemplos de FINALIZADO com DATA CONCLUSÃO vazia. Uma resposta pode descrever vários problemas, e foram observados textos semelhantes em envios distintos. Ainda precisamos confirmar o grão de contagem e o tratamento de possíveis reenvios. Não foram calculados totais ou percentuais a partir dessa leitura parcial.

A aba Página1 contém um resumo publicado com rótulos de situação e uma lista de veículos/espera. Esse resumo não foi reconciliado à base de respostas e não deve ser usado diretamente como indicador mensal por contrato. A ferramenta deve calcular a partir dos registros individuais, com referência pelo mês de abertura e atribuição ao contrato pertinente.

Não foi identificada coluna de contrato na parte inspecionada da aba de respostas. O vínculo por veículo e data dependerá do cadastro/histórico confirmado e poderá gerar pendências. E-mails e nomes dos motoristas não são necessários nos relatórios externos; preservar apenas os dados necessários aos pedidos e seus vínculos.

O usuário confirmou FINALIZADO como critério de conclusão sem exigir DATA CONCLUSÃO e confirmou um envio como uma solicitação, mesmo contendo vários problemas. Possíveis reenvios serão sinalizados para revisão, sem exclusão automática. A observação de que não usam esse campo para controle foi interpretada como dispensar a data de conclusão, mantendo o sim explícito ao uso da situação FINALIZADO. Essa interpretação deve permanecer visível na comunicação.

Ainda é necessário revisar a duração do MTTR: inicialmente foi aceito o tempo integral da OS, mas depois foi confirmado que uma OS pode permanecer aberta com o veículo trabalhando. A escolha entre duração administrativa e intervalo real de imobilização deve ser explícita; não alterar a regra inicial silenciosamente.

O usuário resolveu essa escolha: MTTR usa apenas tempo real em oficina, incluindo esperas durante a imobilização. Fica substituída a definição inicial baseada no tempo administrativo integral da OS. Também aprovou metas configuráveis por contrato, com valores iniciais do relatório de referência: disponibilidade mínima de 90%, MTTR máximo de 18 horas e solicitações atendidas mínimas de 70%. MKBF não recebe classificação de cumprimento enquanto não houver meta definida.

## Publicação da especificação

O usuário invocou a skill to-spec e confirmou como fronteira principal de teste o fluxo completo de seleção, carga, conferência dos quatro indicadores, comentários e HTML, complementado por testes dos cálculos e validação da conexão real ao PCME.

A especificação foi publicada em https://github.com/tropicaltransportes/relatorios/issues/1 com o título “Ferramenta de relatórios de manutenção por contrato com consulta ao PCME e exportação HTML” e rótulo ready-for-agent. A leitura posterior confirmou issue aberta, rótulo aplicado e conteúdo igual ao preparado em `docs/specs/2026-10-19-relatorios-manutencao-issue.md`. A publicação não inclui os exemplos operacionais deste levantamento.