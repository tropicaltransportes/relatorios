# Relatórios de manutenção por contrato

Vocabulário da manutenção e dos relatórios enviados separadamente para cada contrato.

## Language

**Contrato**:
Vínculo associado a um cliente, para o qual é emitido um relatório de manutenção separado. Um veículo pode estar vinculado a contratos diferentes ao longo do período do relatório.

**PCME**:
Sistema utilizado atualmente na operação de manutenção, cujo banco de dados pode ser consultado pelo responsável pelos relatórios.

**Contrato de origem da parada**:
Contrato ao qual o veículo estava vinculado quando a parada começou. É o contrato ao qual a parada é atribuída, mesmo que o veículo mude de contrato antes da liberação.

**Ordem de serviço (OS)**:
Registro de manutenção no PCME que contém datas do serviço e informações de quilometragem do veículo. Pode permanecer aberta enquanto o veículo trabalha; sua duração administrativa não comprova imobilização contínua.

**Solicitação de manutenção**:
Pedido de manutenção feito por um motorista por meio do formulário da planilha de solicitações. Cada envio conta como uma solicitação, mesmo quando descreve vários problemas; não é automaticamente equivalente a uma OS ou a um item de serviço.

**Solicitação atendida**:
Solicitação de manutenção com situação FINALIZADO na planilha, contabilizada no mês em que foi aberta, mesmo quando a conclusão ocorre em um mês posterior. A data de conclusão preenchida não é obrigatória para esse reconhecimento.

**MTTR**:
Tempo médio de reparo calculado a partir do tempo real em oficina dos atendimentos concluídos, incluindo esperas durante a imobilização. O tempo administrativo em que a OS permanece aberta com o veículo trabalhando não integra essa duração.

**Janela de disponibilidade**:
Período em que o veículo deve estar apto à operação: das 5h às 17h, de segunda-feira a sábado.

**Parada elegível**:
Parada com duração de quatro horas ou mais, considerada nos indicadores de disponibilidade e MKBF. As quatro horas são um limite de inclusão, e não uma franquia a ser subtraída da duração.

**Hodômetro de entrada**:
Quilometragem acumulada do veículo registrada na entrada da ordem de serviço; não representa, isoladamente, a distância percorrida no mês.

**Vínculo pendente de confirmação**:
Associação entre veículo, contrato e período cuja informação verbal ainda não permite confirmar o contrato ou as datas envolvidas.

**Falha elegível para o MKBF**:
Ocorrência de falha cujo tempo de reparo atinge quatro horas ou mais. Uma preventiva ou inspeção programada não é automaticamente uma falha, ainda que gere indisponibilidade.