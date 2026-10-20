-- Inspecao somente de metadados: tabelas, views e campos.
-- Abra no DBeaver e selecione a conexao utilizada pelo PCME.
-- Exporte o resultado completo em CSV, incluindo os nomes das colunas.
-- Nao retorna registros operacionais e nao modifica dados.
-- Referencias:
-- https://firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref25/fblangref-appx04-relfields.html
-- https://www.firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref25/fblangref-appx04-fields.html

SELECT
    TRIM(r.RDB$RELATION_NAME) AS TABELA,
    CASE
        WHEN r.RDB$VIEW_BLR IS NULL THEN 'TABELA'
        ELSE 'VIEW'
    END AS TIPO_OBJETO,
    rf.RDB$FIELD_POSITION + 1 AS POSICAO,
    TRIM(rf.RDB$FIELD_NAME) AS CAMPO,
    TRIM(rf.RDB$FIELD_SOURCE) AS DOMINIO,
    f.RDB$FIELD_TYPE AS CODIGO_TIPO,
    f.RDB$FIELD_SUB_TYPE AS SUBTIPO,
    f.RDB$FIELD_LENGTH AS TAMANHO_BYTES,
    f.RDB$CHARACTER_LENGTH AS TAMANHO_TEXTO,
    f.RDB$FIELD_PRECISION AS PRECISAO,
    f.RDB$FIELD_SCALE AS ESCALA,
    CASE
        WHEN COALESCE(rf.RDB$NULL_FLAG, f.RDB$NULL_FLAG, 0) = 1
            THEN 'NAO'
        ELSE 'SIM'
    END AS ACEITA_NULO
FROM RDB$RELATIONS r
JOIN RDB$RELATION_FIELDS rf
    ON rf.RDB$RELATION_NAME = r.RDB$RELATION_NAME
JOIN RDB$FIELDS f
    ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
WHERE COALESCE(r.RDB$SYSTEM_FLAG, 0) = 0
ORDER BY r.RDB$RELATION_NAME, rf.RDB$FIELD_POSITION;