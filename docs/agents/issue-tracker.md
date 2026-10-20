# Issue tracker: GitHub

Tarefas e especificações ficam no GitHub Issues:
https://github.com/tropicaltransportes/relatorios

Use a CLI `gh` com `--repo tropicaltransportes/relatorios`,
inclusive quando a pasta local ainda não tiver um remote configurado.

## Operações

- Criar: gh issue create --repo tropicaltransportes/relatorios --title "..." --body-file <arquivo>
- Consultar: gh issue view <numero> --repo tropicaltransportes/relatorios --comments
- Listar: gh issue list --repo tropicaltransportes/relatorios --state open --json number,title,body,labels
- Comentar: gh issue comment <numero> --repo tropicaltransportes/relatorios --body-file <arquivo>
- Adicionar rótulo: gh issue edit <numero> --repo tropicaltransportes/relatorios --add-label "..."
- Remover rótulo: gh issue edit <numero> --repo tropicaltransportes/relatorios --remove-label "..."
- Encerrar: gh issue close <numero> --repo tropicaltransportes/relatorios

Para textos com várias linhas, use um arquivo com quebras de linha reais.

## Convenções das skills

"Publicar no issue tracker" significa criar uma issue no GitHub.
"Buscar o ticket relevante" significa consultar a issue e seus comentários.

## Pull requests as a triage surface

**PRs as a request surface: no.**