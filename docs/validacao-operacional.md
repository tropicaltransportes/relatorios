# Validação operacional do PCME

Atualizado em 19/10/2026. Esta página separa o que foi verificado localmente do que depende de acesso autorizado ao PCME.

## Verificado no workspace

- O projeto usa o provider `FirebirdSql.Data.FirebirdClient` 10.3.4.
- O teste de conexão consulta a versão do servidor usando o provider efetivamente carregado, exibe ambas as versões e só declara compatibilidade preliminar para Firebird 2.5 a 5.x. O resultado confirma o handshake e a consulta de metadados; a validação com a versão e o esquema reais do PCME continua pendente.
- A descoberta de colunas, a consulta de OS e as leituras usam parâmetros para valores variáveis e transações Firebird com acesso `READ`.
- Os nomes de colunas interpolados nas consultas são escolhidos exclusivamente de listas fixas do código, após conferir os metadados do banco.
- Uma leitura malsucedida não é considerada snapshot vazio. O workspace grava o erro sanitizado e conserva os registros e o horário do último carregamento válido.
- Linhas com identificadores obrigatórios ausentes ou dados de hodômetro inválidos invalidam o carregamento inteiro em vez de desaparecerem silenciosamente. Pendências associadas a snapshots bem-sucedidos são persistidas e mostradas ao operador.
- Rascunhos e snapshots ficam separados pela combinação de contrato e competência. O arquivo de estado anterior (schema 1) é migrado em memória e salvo no novo formato na próxima gravação.
- O resumo e as observações do rascunho são persistidos com essa combinação. O formulário envia a chave da seleção que o operador abriu; revisões antigas recebem conflito em vez de substituir texto mais recente. Escritas entre processos do mesmo perfil são serializadas. Uma conclusão de carregamento vinculada à seleção anterior não é incorporada se o operador já mudou de contrato ou competência.
- A configuração de conexão, incluindo senha, só é gravada após teste bem-sucedido e fica protegida com DPAPI para o usuário Windows atual. A senha não é enviada à página de resultado.

## Campos candidatos, ainda não validados no PCME

Os nomes abaixo vêm das consultas e do levantamento existentes; não representam confirmação de significado operacional:

| Uso | Campo candidato | Situação |
| --- | --- | --- |
| Veículo | `EQUIPAMENTOS.EQUIPAMENTO` | Precisa ser reconciliado com identificadores conhecidos |
| Placa | `EQUIPAMENTOS.PLACA` | Precisa de conferência |
| Identificador de OS | `ORDENS_DE_SERVICOS.OS` | Precisa de conferência |
| Classificação da OS | `ORDENS_DE_SERVICOS.TIPO` | Código não confirma natureza por si só |
| Entrada física | `ORDENS_DE_SERVICOS.DATA_TEM_INI` e `HORA_TEM_INI` | Candidato; precisa comparar com entrada conhecida |
| Liberação física | `ORDENS_DE_SERVICOS.DATA_TEM_FIM` e `HORA_TEM_FIM` | Candidato; precisa comparar com liberação conhecida |
| Quilometragem | `HORIMETRO.HORIMETRO` | Unidade, cobertura e sequência ainda precisam ser verificadas |

O leitor mantém os campos originais da OS e não infere imobilização a partir da abertura, encerramento ou situação administrativa. Colunas opcionais são descobertas pelos metadados; campos obrigatórios ausentes causam falha explícita.

## Pendências de aceitação

- Confirmar a versão real do servidor e a compatibilidade funcional do provider 10.3.4 contra o banco PCME; a faixa 2.5 a 5.x é uma verificação preliminar, não substitui esse teste.
- Testar a conexão com credencial local autorizada, consultar veículos, uma OS conhecida e leituras conhecidas.
- Confirmar entrada e liberação físicas e reconciliar ao menos um registro; demonstrar que uma OS aberta com veículo trabalhando não cria parada contínua.
- Confirmar que o usuário do PCME tem somente permissões de leitura e verificar a ausência de escrita durante a validação autorizada.
- Executar o pacote em um segundo computador Windows da rede.

Nenhuma credencial ou dado operacional real foi incluído no repositório. A ausência de acesso real impede declarar a integração operacional como validada.

## Importação de planilhas e pedidos (20/10/2026)

- A leitura local foi implementada para CSV com campos entre aspas e quebras de linha e para XLSX com leitura sequencial da primeira aba. Células textuais preservam identificadores e zeros à esquerda; datas XLSX formatadas como data permanecem tipadas. Fixtures e casos de teste usam valores sintéticos.
- O mapeamento de cabeçalhos é explícito e valida os campos obrigatórios antes de importar. Leituras de hodômetro são mantidas como registros independentes; não são somadas.
- A identidade local do pedido combina a origem, o carimbo de abertura e o veículo. Situação e posição da linha não integram a identidade. Revisões de conteúdo são separadas do identificador; pedidos semelhantes são mantidos e sinalizados. Colisões de carimbo/veículo interrompem a atualização, mantêm o snapshot anterior e geram pendência com as linhas a revisar, sem mesclar os pedidos. Situação vazia/desconhecida não vira FINALIZADO.
- Uma falha na publicação mantém no resultado os pedidos e a data do snapshot válido anterior, com a falha atual visível. A resposta HTTP e os cabeçalhos são validados antes de aceitar uma nova carga.
- O endpoint CSV é derivado da publicação e aponta para a aba de respostas `gid=1115240100`. A leitura online não foi validada: este ambiente não resolveu `api.nuget.org` e não teve conectividade de rede disponível para confirmar a publicação. Não foi usado arquivo de exportação operacional.
- Nenhum acesso a planilhas com registros reais foi usado. A importação deve ser reconciliada pelo operador com uma resposta conhecida e com a ordem de colunas da publicação antes de ser considerada aceita em operação.
- O SDK .NET 10 compilou Core, Infrastructure e o novo projeto de testes Infrastructure. A execução do filtro VSTest iniciou a descoberta, mas ficou sem resposta e precisou ser interrompida; portanto, os casos automatizados não foram contabilizados como aprovados nesta execução. A validação completa da suíte permanece pendente.
- Como o cache local não contém CsvHelper nem ExcelDataReader e a restauração do NuGet falhou por indisponibilidade de DNS, os leitores usam as APIs nativas do .NET. A troca para essas dependências só deve ser feita quando a restauração estiver disponível e após validar a compatibilidade dos formatos.

## Validação local do workspace (19/10/2026)

- Foram adicionados testes automatizados para isolamento das notas entre contratos e competências, conflito em gravações concorrentes de instâncias separadas, preservação de notas e snapshot/pendências após falha de atualização e descarte de uma carga atrasada quando a seleção ativa mudou. O runner VSTest deste ambiente não completou a inicialização dos casos.
- A aplicação expõe no workspace ativo a data do último sucesso por fonte, os erros sanitizados, a contagem de registros preservados e as pendências retornadas pelo carregamento.
- A execução desta validação não teve acesso a uma conexão autorizada ao PCME. Os campos candidatos, permissões somente leitura e semântica de entrada/liberação continuam pendentes de reconciliação com registros reais.
## Disponibilidade e MTTR (19/10/2026)

- Validação local executada com dados sintéticos nos testes automatizados da Tarefa 3.
- Nenhum acesso autenticado ao PCME foi disponibilizado nesta execução; portanto, campos de entrada/liberação física e sua correspondência com ocorrências reais continuam sem validação operacional.
- Antes de usar os indicadores com registros reais, conferir no PCME pelo menos uma parada física conhecida, sua entrada e liberação, uma OS que permaneceu aberta enquanto o veículo operava e a reconciliação da competência de início no MTTR.
- Esta execução não acessou nem alterou o PCME. Nenhum resultado operacional real foi calculado.