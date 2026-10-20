# Ferramenta de relatórios de manutenção por contrato

Proposta para revisão, consolidada em 19/10/2026. O fluxo, as fontes e as principais regras foram discutidos com o usuário. As políticas de tratamento e os critérios de aceite abaixo tornam o desenho verificável e podem ser ajustados nesta revisão, antes do plano de implementação.

## Objetivo

Permitir que a equipe gere, quando precisar, um relatório de manutenção separado por contrato e mês, reduzindo consultas ao PCME e cruzamentos manuais. Os destinatários são os responsáveis pelos contratos associados aos clientes.

O resultado será um arquivo HTML independente com indicadores, ranking, comentários e identificação do período. Os números devem ser conferíveis pelos registros que os originaram, e informações ausentes devem aparecer como pendências ou resultados indisponíveis, sem serem convertidas em zero.

## Forma de uso

A ferramenta será distribuída por uma pasta compartilhada do OneDrive e executada em computadores Windows da mesma rede do PCME. Cada pessoa abrirá uma tela no navegador, atendida por um processo local iniciado pela ferramenta. A interface não exige uso do DBeaver ou comandos SQL na rotina de geração.

O pacote distribuído deve incluir os componentes necessários para iniciar a ferramenta, sem depender da instalação de um ambiente de desenvolvimento nos outros computadores. A escolha da linguagem e das bibliotecas pertence ao plano técnico, condicionada à compatibilidade com a versão real do servidor Firebird.

Cada usuário terá rascunhos, complementos manuais e configurações de trabalho próprios. Não haverá edição simultânea compartilhada. Dados de trabalho ficarão em pasta separada por usuário/computador, para evitar sobrescrever o trabalho de outro operador; uma atualização do pacote não poderá apagá-los. Configuração de conexão e credenciais ficarão locais ao computador, fora da pasta compartilhada de distribuição.

Os HTMLs emitidos poderão ser salvos na pasta compartilhada. Cada emissão terá nome identificando contrato, competência, data/hora e um identificador que evite colisões entre computadores. Um arquivo já emitido não será alterado por uma atualização dos dados.

## Fluxo principal

1. Selecionar contrato e mês de referência.
2. Atualizar os dados disponíveis, identificando a data/hora de atualização de cada fonte.
3. Conferir indicadores, detalhes por veículo e pendências que afetem o resultado.
4. Informar ou revisar comentários em campos de resumo do mês, ocorrências por veículo e próximas ações. Salvar o rascunho para continuar depois.
5. Visualizar o relatório e gerar o HTML. Permitir selecionar vários contratos, mantendo um arquivo e um conjunto de comentários separados para cada um.

Se uma fonte estiver indisponível, o usuário poderá visualizar o último carregamento bem-sucedido com sua data identificada. A falha de atualização permanecerá visível; o último carregamento não será apresentado como atual.

## Fontes e responsabilidades

| Fonte | Dados utilizados | Papel |
| --- | --- | --- |
| PCME / Firebird | Veículos, OS, classificação, entrada em oficina, liberação e leituras disponíveis | Fonte dos registros da manutenção, consultada sem alterações |
| INDICADORES - OCUPAÇÃO (v1).xlsx | Data, hora, veículo, contrato e hodômetro | Complemento de quilômetros e evidência de operação |
| Google Sheets publicado SOLICITAÇÃO DE SERVIÇOS | Pedidos dos motoristas, data de abertura, veículo, descrição e situação | Fonte do indicador de solicitações |
| Cadastro manual do operador | Vínculos de veículo com contrato e período, confirmações e metas | Complemento quando as fontes não determinarem a atribuição |

A planilha publicada de solicitações será configurada pelo endereço fornecido, com leitura da aba de respostas individuais. A aba de resumo não será a base de cálculo mensal. Prever importação de arquivo CSV/XLSX exportado da mesma fonte quando a publicação não puder ser lida. Preservar a origem e a data de cada carregamento.

Os identificadores de veículo serão tratados como texto: preservar zeros iniciais e sufixos como V ou M. A associação entre fontes usará identificadores confirmados; apelidos, placas e diferenças de grafia serão resolvidos por um cadastro de correspondências. Ambiguidades serão mostradas para correção, sem escolher arbitrariamente um veículo.

## Contratos e frota prevista

O cadastro de vínculos terá veículo, contrato, início, fim quando conhecido e estado de confirmação. Informações verbais poderão ser registradas pelo operador. Sem data ou contrato suficiente, a atribuição ficará pendente.

A planilha de operação poderá sugerir associações para conferência, mas a ausência de apontamento não determinará que o veículo saiu do contrato. As movimentações de lotação do PCME só poderão substituir ou complementar o cadastro após confirmação de que representam contratos e possuem histórico confiável.

O total de horas previstas de um contrato será calculado por veículo e pelo trecho confirmado de sua alocação no mês. Não usar apenas a quantidade atual de veículos multiplicada pelo mês inteiro.

Uma parada será atribuída ao contrato em que começou, mesmo quando a liberação ocorrer depois de uma transferência. Se essa atribuição produzir horas fora da cobertura confirmada do contrato de origem, mostrar a inconsistência para confirmação do período de responsabilidade antes de consolidar o indicador. Não cortar horas nem limitar percentuais artificialmente para ocultar o conflito.

## Registro físico da oficina

OS administrativa e imobilização física são conceitos separados. O banco já mostrou OS abertas para veículos com registros posteriores de operação; portanto, abertura da OS e ausência de encerramento não bastam para inferir parada contínua.

A integração deverá produzir intervalos reais de entrada e liberação. Cada intervalo será associado ao veículo, à OS quando disponível e à sua origem. Os campos candidatos do PCME serão validados com registros reais antes de serem usados como fonte automática desses intervalos. DATA_FIM, DATA_ENCERRA, campos temporários e indicadores de situação não serão considerados equivalentes apenas por seus nomes.

Quando não for possível determinar um intervalo, apresentá-lo como pendência e permitir confirmação manual identificada. Um registro manual não alterará o PCME. Para veículo efetivamente imobilizado e ainda não liberado, usar o horário da consulta como fim provisório, deixando esse estado visível.

Se houver vários intervalos ou OS simultâneas do mesmo veículo, a disponibilidade usará a união dos intervalos para não descontar a mesma hora duas vezes. No MTTR, registros potencialmente referentes ao mesmo atendimento físico exigirão identificação do atendimento, sem duplicar sua duração por item de serviço.

## Indicadores

### Disponibilidade física

A janela prevista será das 5h às 17h, de segunda-feira a sábado, no horário local da operação. Na primeira versão, feriados seguirão o dia da semana; não será aplicada uma agenda adicional de exceções.

Uma parada física com duração total igual ou superior a quatro horas será elegível. As quatro horas são um filtro de inclusão, não uma franquia. Uma parada de seis horas que ocorra inteiramente dentro da janela prevista descontará seis horas; exatamente quatro horas também será incluída.

Para cada parada elegível, descontar somente as horas coincidentes com a janela prevista. Distribuir essas horas pelos meses em que ocorreram, mantendo a atribuição ao contrato de origem.

Disponibilidade = (horas previstas − horas indisponíveis elegíveis) / horas previstas × 100.

Sem frota/período confirmado ou com denominador zero, apresentar resultado indisponível com a causa. Um veículo sem parada registrada não comprova cobertura se os dados de manutenção estiverem ausentes.

### MTTR

Usar o tempo real em oficina dos atendimentos concluídos, incluindo espera por peças e atendimento durante a imobilização. Não limitar o MTTR à janela de 5h às 17h, nem incluir tempo administrativo com o veículo trabalhando.

MTTR = soma das durações em oficina dos atendimentos concluídos / quantidade desses atendimentos.

Manter a competência pelo mês de início do atendimento, conforme a escolha de referência pela abertura discutida com o usuário. Se o atendimento começar em junho e terminar em julho, atualizar junho quando a duração final se tornar conhecida. Um atendimento ainda aberto não terá duração final no MTTR.

Não aplicar automaticamente ao MTTR o filtro de quatro horas, escolhido para disponibilidade e MKBF. Sem atendimento concluído válido, apresentar sem ocorrências para cálculo, não zero horas como resultado de reparo.

### MKBF

Distinguir falha de manutenção programada. Preventivas e inspeções não serão automaticamente falhas, mesmo quando reduzirem disponibilidade. A classificação da OS poderá sugerir a natureza da ocorrência, mas casos ambíguos precisarão de confirmação.

Contar as ocorrências de falha cujo tempo real de reparo em oficina atinja quatro horas. Uma mesma ocorrência não será multiplicada pela quantidade de serviços da OS. A referência da ocorrência será o início do atendimento; enquanto a duração for provisória, indicar que o resultado pode mudar.

MKBF = quilômetros validados da população e do período / quantidade de falhas elegíveis da mesma população e período.

Hodômetros são leituras acumuladas. Não somar leituras nem tratar uma leitura de entrada de OS como quilômetros rodados. Validar ordem temporal, unidade, quedas e saltos de leitura antes de obter diferenças. Não extrapolar distâncias para dias sem medição.

Mostrar a cobertura por veículo e período. Para publicar um MKBF consolidado do contrato, exigir distância validada para toda a frota/período incluído; os casos sem base suficiente serão mostrados como indisponíveis, com o que falta, podendo ser complementados por informação manual confirmada. Resultados parciais por veículo não serão apresentados como MKBF completo do contrato.

Com quilômetros válidos e nenhuma falha elegível, mostrar que não houve falha elegível; não apresentar zero ou infinito como se fossem uma distância medida entre falhas.

### Solicitações atendidas

Uma resposta ao formulário será uma solicitação, mesmo contendo vários problemas. O mês será o do carimbo de abertura e o contrato será determinado pelo veículo e vínculo vigente nessa data.

Percentual = solicitações do mês com situação FINALIZADO / total de solicitações válidas abertas no mês × 100.

FINALIZADO será suficiente mesmo sem DATA CONCLUSÃO preenchida. PENDENTE e EM ANDAMENTO não contarão como concluídas. Situações vazias ou desconhecidas serão sinalizadas, sem serem interpretadas como conclusão. Se o pedido puder ser atribuído ao período e contrato, ele continuará no total de pedidos.

Uma conclusão posterior atualizará a competência de abertura. Não tentar reconstruir a situação de uma emissão passada a partir da situação atual da planilha; o HTML daquela emissão preservará a versão publicada.

Possíveis reenvios serão sinalizados para conferência, sem exclusão automática. A importação não poderá duplicar pedidos a cada atualização nem depender da posição da linha, que muda quando novas respostas entram. Alterar a situação de um pedido não poderá criar outro pedido na base local.

Se não houver solicitações válidas no período, apresentar sem solicitações e deixar o percentual sem classificação, em vez de atribuir 0% ou 100%.

### Metas

Metas configuráveis por contrato, com valores iniciais propostos pelo relatório e aceitos pelo usuário: disponibilidade mínima de 90%, MTTR máximo de 18 horas e solicitações atendidas mínimas de 70%.

MKBF será apresentado sem classificação de cumprimento enquanto não existir meta definida. Resultado indisponível ou sem base não será classificado como conforme. Preservar os valores de meta utilizados em cada emissão para que mudanças futuras não alterem o HTML emitido.

## Conteúdo do HTML

Identificar contrato, mês, frota coberta, autor, emissão e atualização das fontes. Apresentar os indicadores com unidades e metas, o ranking por indisponibilidade e os comentários do operador. Permitir imprimir pelo navegador.

O arquivo deverá abrir sem conexão com o banco ou com a ferramenta, usando estilos e recursos necessários contidos no próprio HTML. Não incluir credenciais, caminhos internos, e-mails de motoristas ou dados de outros contratos. Textos das fontes e comentários devem ser tratados como texto, sem executar HTML ou scripts que tenham sido inseridos nesses campos.

Indicadores sem base suficiente terão motivo explícito. Antes da emissão, o usuário verá as pendências. Será possível gerar um relatório parcial, identificado como tal, mantendo indisponíveis os resultados não sustentados; o arquivo não poderá parecer completo quando faltarem dados materiais.

## Componentes e limites

- Interface local: seleção, revisão, comentários, configuração e emissão.
- Leitor do PCME: consultas somente leitura e mapeamento para os registros de manutenção, sem gravar no sistema de origem.
- Importadores de planilhas: leitura, validação e correspondência dos registros de operação e solicitações.
- Cálculos: funções independentes das fontes, recebendo intervalos físicos, quilômetros, vínculos e pedidos já normalizados.
- Armazenamento individual: rascunhos, complementos, correspondências, configurações e referências dos carregamentos.
- Emissor HTML: gera uma cópia independente dos resultados e comentários revisados.

Cada componente terá um contrato de dados próprio. Trocar o formato de uma planilha ou corrigir um campo do PCME não deverá exigir reescrever os cálculos dos indicadores.

## Critérios de aceite

1. Um operador consegue selecionar contrato/mês, conferir pendências, comentar, salvar e emitir sem executar SQL manualmente.
2. A ferramenta abre num segundo computador da rede a partir do pacote distribuído, após configurar o acesso local ao banco.
3. Trabalhos em dois computadores não sobrescrevem rascunhos ou credenciais de outro operador.
4. Uma OS aberta com veículo trabalhando não produz imobilização contínua por esse motivo.
5. Casos de quatro horas exatas, menos de quatro horas, parada noturna, domingo, mês cruzado e OS sobrepostas têm resultados coerentes com as regras acima.
6. MTTR considera somente o tempo real em oficina dos atendimentos concluídos e mantém a competência definida.
7. Preventiva e inspeção programada não entram automaticamente como falha no MKBF; quilômetros ausentes não viram zero.
8. Atualizar a planilha de pedidos não duplica registros; FINALIZADO atualiza o mês de abertura, mesmo sem data de conclusão.
9. Atribuições de contrato incertas e leituras inconsistentes ficam identificadas, sem seleção automática arbitrária.
10. Um HTML abre em outro computador sem consultar as fontes, contém apenas seu contrato e preserva os dados daquela emissão.
11. Uma amostra de resultados é reconciliada com registros conhecidos e com o relatório de referência, explicando diferenças causadas pelas regras acordadas; reproduzir um número antigo não substitui validar sua fórmula.

## Sequência de implementação proposta

Primeiro validar a conexão de consulta e o mapeamento do intervalo físico no PCME, preservando os exports recebidos como evidência de descoberta. A versão do servidor será obtida pela própria conexão.

Depois implementar os modelos normalizados e os cálculos com casos concretos, os importadores, o cadastro complementar e o fluxo de revisão/emissão. Concluir com empacotamento Windows e validação no segundo computador. Acesso ao banco ou leitura de uma fonte que ainda não esteja disponível não será declarado funcional por testes com dados simulados.

Esta sequência orienta a revisão do desenho. O plano técnico detalhado será elaborado após sua aprovação.