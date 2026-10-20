## Problem Statement

A equipe de manutenção precisa enviar relatórios separados para os contratos associados aos clientes. O PCME não oferece uma extração simples das informações necessárias: o responsável faz várias consultas, cruza dados em planilhas e monta manualmente os indicadores e a análise do mês.

Os veículos podem mudar de contrato durante o período. Parte do histórico de vínculos vem de informação verbal, os quilômetros podem estar incompletos e pedidos dos motoristas são registrados em outra planilha. Uma OS pode continuar aberta enquanto o veículo trabalha; tratar sua duração administrativa como imobilização distorceria os indicadores.

## Solution

Criar uma ferramenta Windows de uso sob demanda, distribuída pelo OneDrive e utilizada em computadores da mesma rede. Cada operador abre uma interface no navegador, seleciona contrato e mês, atualiza os dados, confere os indicadores e pendências, escreve os comentários e gera um arquivo HTML independente.

A ferramenta consulta diretamente o banco Firebird do PCME usando uma conexão própria somente de leitura. O DBeaver não precisa estar aberto e exportações manuais de OS não fazem parte da rotina. A planilha de operação complementa os quilômetros; a planilha publicada de solicitações fornece os pedidos e suas situações. Cada operador mantém rascunhos e configurações individuais, sem edição compartilhada.

## User Stories

1. Como responsável pela manutenção, quero gerar um relatório quando precisar, para atender cada contrato sem depender de um fechamento ou rotina agendada.
2. Como operador, quero selecionar contrato e mês de referência, para delimitar os dados do relatório.
3. Como operador, quero selecionar vários contratos e gerar arquivos separados, para preparar as entregas sem misturar clientes.
4. Como operador, quero consultar o banco do PCME pela própria ferramenta, para dispensar consultas manuais no DBeaver e exportações de OS na rotina.
5. Como operador, quero configurar e testar a conexão de leitura, para saber se o computador consegue acessar o PCME.
6. Como responsável pelo sistema de origem, quero que a integração não altere o PCME, para preservar a operação atual.
7. Como integrante da equipe, quero abrir a ferramenta distribuída pelo OneDrive em outro computador da mesma rede, para trabalhar sem instalar um ambiente de desenvolvimento.
8. Como operador, quero manter meus próprios rascunhos e configurações, para trabalhar sem sobrescrever o trabalho de outra pessoa.
9. Como operador, quero atualizar a ferramenta preservando meus dados locais, para continuar meus relatórios após uma atualização.
10. Como operador, quero carregar os apontamentos da planilha de operação, para aproveitar as leituras de quilômetros já registradas.
11. Como operador, quero ler os pedidos dos motoristas na planilha publicada, para apurar solicitações sem confundi-las com serviços internos da oficina.
12. Como operador, quero importar uma exportação CSV ou XLSX quando a publicação estiver indisponível, para continuar o trabalho com uma fonte identificada.
13. Como operador, quero ver a data da última atualização de cada fonte, para avaliar a atualidade dos números.
14. Como operador, quero receber uma indicação de falha e consultar o último carregamento válido, para não perder o trabalho nem confundir dados antigos com atuais.
15. Como operador, quero preservar identificadores de veículos com zeros iniciais e letras, para cruzar corretamente as fontes.
16. Como operador, quero confirmar correspondências entre códigos, placas e apelidos, para resolver diferenças de identificação sem associações arbitrárias.
17. Como operador, quero cadastrar e confirmar o vínculo do veículo com contrato e período, para produzir relatórios históricos mesmo quando essa informação vier de uma fonte verbal.
18. Como operador, quero ver vínculos ausentes ou conflitantes, para corrigir a atribuição antes de consolidar os indicadores.
19. Como responsável por um contrato, quero que a parada pertença ao contrato em que começou, para respeitar a responsabilidade acordada mesmo quando houver transferência posterior.
20. Como responsável por um contrato, quero que as horas previstas considerem o período real de alocação de cada veículo, para não cobrar disponibilidade antes da entrada ou após a saída.
21. Como operador, quero distinguir OS aberta de veículo fisicamente parado, para não descontar disponibilidade enquanto o veículo trabalha.
22. Como operador, quero confirmar intervalos de oficina não determinados pelas fontes, para resolver pendências sem alterar o PCME.
23. Como responsável por um contrato, quero disponibilidade calculada na janela de 5h às 17h, de segunda a sábado, para refletir a obrigação operacional definida.
24. Como responsável por um contrato, quero considerar paradas de quatro horas ou mais sem descontar uma franquia, para aplicar o critério acordado.
25. Como responsável por um contrato, quero que uma parada que atravesse meses desconte as horas em cada mês correspondente, para evitar concentrar indisponibilidade em uma competência.
26. Como operador, quero evitar desconto duplicado de horas em OS sobrepostas, para não superestimar a indisponibilidade do mesmo veículo.
27. Como responsável por um contrato, quero MTTR baseado apenas no tempo real em oficina dos atendimentos concluídos, para medir reparo e espera durante a imobilização.
28. Como operador, quero separar falhas de preventivas e inspeções programadas, para que o MKBF represente falhas elegíveis.
29. Como operador, quero quilômetros calculados a partir de leituras ou totais confirmados, para não somar hodômetros nem inventar distância ausente.
30. Como responsável por um contrato, quero ver a cobertura de quilômetros e o motivo de um MKBF indisponível, para não interpretar um resultado parcial como completo.
31. Como operador, quero que cada envio do motorista conte como uma solicitação, para manter a unidade de contagem mesmo quando ele descreva vários problemas.
32. Como operador, quero reconhecer FINALIZADO como conclusão mesmo sem data de conclusão preenchida, para aproveitar a informação efetivamente usada na planilha.
33. Como responsável por um contrato, quero que uma solicitação concluída depois atualize o mês de abertura, para acompanhar a resolução dos pedidos daquela competência.
34. Como operador, quero que atualizações e reordenação da planilha não dupliquem pedidos, para manter os percentuais corretos.
35. Como operador, quero conferir possíveis reenvios e situações desconhecidas, para tratar inconsistências sem exclusão automática.
36. Como operador, quero ajustar as metas por contrato, para comparar os indicadores com os valores pertinentes.
37. Como operador, quero consultar o ranking de veículos por indisponibilidade, para explicar as ocorrências mais relevantes.
38. Como operador, quero escrever o resumo do mês, comentários por veículo e próximas ações, para complementar os números com o conhecimento da operação.
39. Como operador, quero salvar e retomar o rascunho, para elaborar o relatório em momentos diferentes.
40. Como operador, quero visualizar o relatório antes de emitir, para conferir números, pendências e comentários.
41. Como operador, quero emitir um relatório parcial claramente identificado quando faltarem dados, para comunicar o que está disponível sem apresentar valores sem base.
42. Como destinatário, quero abrir o HTML recebido sem acesso ao banco ou à ferramenta, para consultar o relatório diretamente no navegador.
43. Como destinatário, quero imprimir o relatório pelo navegador, para obter uma cópia legível quando necessário.
44. Como responsável por um contrato, quero que o arquivo emitido preserve os números e as metas daquela emissão, para distinguir a entrega original de revisões posteriores.
45. Como operador, quero nomes de arquivo que evitem colisões entre emissões, para salvar entregas no OneDrive sem sobrescrever arquivos de outro usuário.
46. Como responsável pelas informações, quero que cada arquivo contenha somente seu contrato e os dados necessários ao relatório, para evitar a inclusão de credenciais, e-mails de motoristas ou registros de outros clientes.
47. Como operador, quero conferir os registros que sustentam cada indicador, para explicar diferenças e validar o resultado antes do envio.

## Implementation Decisions

- **Execução local:** um processo Windows inicia a interface apenas em loopback e abre o navegador. O pacote inclui os componentes necessários para o usuário final; não há servidor central ou dependência de SDK na máquina do operador.
- **Fronteiras dos módulos:** leitor do PCME; importadores de operação e solicitações; normalização e correspondências; cálculos; composição do relatório; persistência individual; emissão HTML. Os cálculos recebem registros normalizados e não conhecem SQL nem o formato físico das planilhas.
- **Caso de uso principal:** preparar um relatório por contrato/mês, atualizar suas fontes, expor resultados e pendências, salvar comentários e emitir uma cópia independente. A seleção de vários contratos reutiliza esse caso de uso separadamente para cada contrato.
- **Direção técnica proposta no plano existente:** .NET 10 LTS, ASP.NET Core com Razor Pages, provider .NET do Firebird, leitura CSV/XLSX e armazenamento individual em JSON. Validar compatibilidade do provider com a versão efetiva do servidor antes de declarar a conexão funcional.
- **Conexão:** configuração local de servidor, porta, base, usuário e autenticação, com teste de acesso, timeout e mensagens sanitizadas. Não descriptografar ou depender do cofre do DBeaver. Consultas parametrizadas em transação de leitura; nenhuma alteração de esquema ou dado do PCME.
- **Histórico e carregamentos:** cada fonte preserva a data da última leitura bem-sucedida e seu erro mais recente. Uma falha mantém o snapshot anterior identificado; nunca transforma falha de leitura em base vazia aparentemente válida.
- **Planilhas:** ler os registros individuais das respostas dos motoristas, não os totais da aba de resumo. Permitir mapeamento explícito quando um cabeçalho estiver vazio ou diferente e importação CSV/XLSX da mesma fonte como alternativa. A configuração privada guarda os endereços e arquivos operacionais.
- **Identificadores:** preservar códigos de veículo como texto, incluindo zeros iniciais e letras. Correspondências ambíguas exigem confirmação; posição da linha, nomes semelhantes ou contrato atual não bastam para determinar histórico.
- **Vínculos com contratos:** registrar veículo, contrato, início, fim quando conhecido e estado de confirmação. Operação registrada pode sugerir um vínculo, mas dia sem apontamento não comprova saída. Histórico de lotação do PCME só automatiza o vínculo depois de validada sua equivalência e cobertura.
- **Contrato de origem da parada:** a parada permanece atribuída ao contrato em que começou. Quando ela ultrapassa a cobertura confirmada desse contrato, apresentar a inconsistência para confirmar o período de responsabilidade, em vez de cortar horas ou limitar artificialmente o percentual.
- **Intervalo físico:** separar entrada/liberação reais da abertura e do encerramento administrativos da OS. O mapeamento dos campos do PCME precisa ser validado com registros reais; nomes de campo ou status aberto não comprovam imobilização. Quando os dados não sustentarem um intervalo, expor pendência e permitir complemento manual identificado, sem escrever no PCME.
- **Oficina em andamento:** somente um veículo confirmado como fisicamente imobilizado pode usar o horário da consulta como fim provisório. Uma OS aberta com o veículo trabalhando não gera parada contínua.
- **Calendário de disponibilidade:** 5h às 17h, de segunda-feira a sábado, no horário da operação. Na primeira versão, feriados seguem o dia da semana.
- **Elegibilidade:** parada física com duração igual ou superior a quatro horas. É filtro, não franquia: uma parada de seis horas inteiramente na janela prevista desconta seis, e quatro horas exatas entram.
- **Disponibilidade:** calcular (horas previstas menos horas indisponíveis elegíveis) dividido por horas previstas, vezes 100. O denominador considera a alocação confirmada de cada veículo no mês; o numerador usa a união de intervalos coincidentes com a janela prevista, sem duplicar horas sobrepostas. Dividir as horas pelos meses em que ocorreram. Dados insuficientes ou denominador zero geram resultado indisponível, não conformidade presumida.
- **MTTR:** média do tempo real em oficina dos atendimentos concluídos, incluindo esperas durante a imobilização e horas fora da janela de disponibilidade. Não usar o tempo administrativo da OS, não aplicar automaticamente o filtro de quatro horas e não multiplicar o atendimento por seus itens de serviço. A competência é a do início do atendimento e atualiza quando sua duração final for conhecida.
- **Falhas para MKBF:** distinguir falha de manutenção programada. Preventivas e inspeções não são automaticamente falhas. Contar uma ocorrência de falha cujo reparo físico atinja quatro horas, usando a competência do início; natureza ambígua exige confirmação.
- **Quilômetros:** hodômetro é leitura acumulada, não distância mensal. Validar sequência temporal, unidade e inconsistências antes de calcular diferenças. Não extrapolar dias ausentes. Permitir total manual confirmado com identificação de origem quando necessário.
- **MKBF:** quilômetros validados divididos por falhas elegíveis da mesma população e período. Consolidado exige cobertura de toda a frota/período incluído; resultados parciais por veículo não representam o contrato completo. Falta de quilômetros gera resultado indisponível; ausência de falhas elegíveis com quilômetros válidos aparece como ausência de ocorrência, não como zero ou infinito.
- **Solicitações:** cada envio do formulário é um pedido, mesmo com vários problemas. Percentual de atendimento é o número de pedidos FINALIZADO dividido pelo total de pedidos válidos abertos no mês, vezes 100, atribuídos ao contrato vigente na abertura.
- **Conclusão do pedido:** FINALIZADO basta mesmo sem data de conclusão. PENDENTE e EM ANDAMENTO não contam como concluído. Situação vazia ou desconhecida gera pendência; um pedido atribuível ao contrato e período continua no denominador. Nenhum pedido gera indicação sem solicitações, não 0% ou 100%.
- **Atualizações e reenvios:** conclusão posterior atualiza o mês de abertura. Reimportação, reordenação de linhas e mudança de situação não criam novo pedido. Possíveis reenvios ficam para conferência, sem exclusão automática. Identidades ambíguas exigem resolução em vez de juntar pedidos distintos.
- **Metas:** configuráveis por contrato; valores iniciais de disponibilidade mínima 90%, MTTR máximo 18 horas e solicitações atendidas mínimas 70%. MKBF não recebe classificação de cumprimento sem meta definida. Resultados sem base suficiente não aparecem como conformes.
- **Rascunhos e complementos:** manter contratos, correspondências, confirmações, metas e comentários individuais, com salvamento que preserve o último estado válido. Detectar revisões concorrentes no mesmo perfil, mesmo sem colaboração entre operadores.
- **Comentários:** campos próprios para resumo do mês, ocorrências por veículo e próximas ações. Atualizar dados não deve apagar os comentários já escritos.
- **Emissão HTML:** incluir contrato, competência, frota coberta, autor, emissão, atualização das fontes, indicadores, metas, ranking e comentários. Estilos e recursos necessários ficam incorporados; o arquivo abre sem banco, backend ou Internet e pode ser impresso.
- **Versões e isolamento:** cada emissão preserva números, metas e comentários daquele momento e recebe nome sem colisão. Não sobrescrever emissão anterior nem incluir dados de outro contrato. Mostrar resultados indisponíveis e identificar relatório parcial quando houver ausência material.
- **Tratamento do conteúdo:** nomes, descrições e comentários entram como texto codificado, sem executar conteúdo de planilhas. Credenciais ficam protegidas no computador; não entram na pasta compartilhada, nos logs, no HTML ou em arquivos de configuração exportáveis.

## Testing Decisions

- **Fronteira principal confirmada pelo usuário:** testar o fluxo observável completo da aplicação: escolher contrato/mês, carregar dados controlados, conferir os quatro indicadores e pendências, salvar e recuperar comentários, visualizar e gerar o HTML. Exercitar a interface pública do caso de uso pela aplicação, substituindo apenas as fontes externas nos testes determinísticos.
- **Qualidade dos testes:** verificar comportamento e resultados que o operador ou destinatário observa. Não espelhar o código, contar chamadas internas ou testar detalhes privados sem relação com a regra de negócio.
- **Complementos confirmados:** testes focados dos cálculos, porque limites temporais e agregações precisam de resultados exatos, e validação separada da conexão real ao PCME. Testes com substitutos de fontes não comprovam acesso ao banco, publicação online ou outro computador.
- **Disponibilidade:** verificar quatro horas exatas, duração inferior, seis horas sem franquia, madrugada, domingo, mudança de mês, alocação parcial, contrato de origem, transferência com cobertura inconsistente e OS sobrepostas sem desconto duplicado.
- **MTTR:** verificar apenas duração física de atendimentos concluídos, espera em oficina incluída, trabalho fora da oficina excluído, atendimento aberto fora da média final, competência de início e ausência de duplicação por item de serviço.
- **MKBF:** verificar preventiva/inspeção fora da contagem automática de falha, falha física elegível, natureza desconhecida, hodômetro acumulado, unidade incorreta, queda de leitura, cobertura parcial e ausência de falhas. Dados ausentes nunca viram distância zero.
- **Solicitações:** verificar um envio com vários problemas, FINALIZADO sem data de conclusão, atualização de competência anterior, situação desconhecida, nenhum pedido, mudança de ordem das linhas, reimportação idempotente, alteração da situação e reenvios possíveis sem exclusão automática.
- **Importação e falhas:** verificar CSV com quebras de linha e acentos, XLSX com valores tipados, IDs com zero e sufixo, colunas reordenadas, cabeçalho vazio, publicação com resposta inesperada e arquivo sincronizado incompleto/bloqueado. Preservar o último carregamento válido e mostrar sua data.
- **Persistência:** verificar retomada do rascunho, manutenção dos comentários após atualizar fontes, perfis de usuários isolados, conflito de revisão e falha durante gravação sem perda do estado anterior.
- **HTML:** verificar abertura independente, impressão legível, ausência de recursos externos, isolamento por contrato, texto malicioso tratado como texto, ausência de credenciais/e-mails e preservação de uma emissão após atualização posterior.
- **Aceitação operacional:** testar a leitura real de registros conhecidos do PCME, conferir entrada/liberação físicas e uma solicitação conhecida, reconciliar uma amostra dos indicadores e executar o pacote num segundo computador da rede sem SDK. Registrar evidência de cada verificação; não marcar como concluída uma validação externa indisponível.
- **Prior art:** o projeto local ainda contém apenas documentação e consultas de descoberta, sem aplicação ou suíte de testes existente. Não há teste anterior a reutilizar. O fluxo completo será a referência principal e os casos de cálculo serão cenários sintéticos rastreáveis às regras discutidas; não publicar amostras operacionais reais no repositório.

## Out of Scope

- Alterar dados, estrutura, procedimentos ou permissões do banco do PCME.
- Substituir o PCME ou a planilha dos motoristas como sistema de controle operacional.
- Criar pedidos, enviar mensagens a motoristas ou automatizar comunicações com clientes.
- Edição simultânea compartilhada de rascunhos, portal de clientes, autenticação de destinatários ou hospedagem de relatórios por link.
- Servidor central, banco de trabalho concorrente sincronizado pelo OneDrive ou operação fora da rede sem acesso autorizado ao PCME.
- Inventar históricos de contrato, distâncias, conclusão de pedidos ou intervalos físicos ausentes.
- Escrita automática de análise narrativa, estimativa silenciosa de quilômetros ou metas de MKBF não definidas.
- Agenda especial de feriados, períodos personalizados além da competência mensal, custos financeiros de manutenção ou indicadores além dos quatro acordados.
- Modificar a planilha de origem como parte da importação, excluir reenvios automaticamente ou exigir preenchimento de data de conclusão para contar FINALIZADO.

## Further Notes

O desenho funcional e o plano técnico já foram registrados durante a conversa. Esta issue consolida os requisitos para implementação; não afirma que a aplicação, a conexão direta ou as validações operacionais já estejam concluídas.

O esquema e as amostras recebidos apontam candidatos para OS, veículos, leituras e movimentação de lotação. Ainda é necessário validar os campos que representam a oficina física, a versão do servidor, o acesso de leitura e a confiabilidade do histórico de vínculos. A existência de uma tabela ou de um código não comprova seu significado operacional. A captura de tela do PCME foi dispensada pelo usuário e não é pré-requisito.

Solicitações vêm da planilha dos motoristas. Os registros de serviços pendentes do banco não foram confirmados como equivalentes aos pedidos; não substituir uma fonte pela outra sem evidência.

A especificação publicada não inclui endereços privados de conexão, credenciais, links das planilhas operacionais ou exemplos reais de clientes, veículos e motoristas. Esses dados devem ser configurados localmente para a validação.
