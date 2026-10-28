# TestPlayground

Greybox em escala de metros para Unity **6000.6.7f1**, com **URP 17.6.0**. O projeto original define os assets URP em `QualitySettings` (PC e Mobile), embora o pipeline global em `GraphicsSettings` esteja vazio. O gerador usa o pipeline efetivamente ativo, sem alterar essas configurações, pacotes, Input System, cenas existentes ou a lista de cenas do build.

Não foi encontrado `AGENTS.md` no projeto ou nas pastas superiores verificadas antes da implementação.

## Abrir e gerar

1. Abra este projeto na Unity 6000.6.7f1 e aguarde a importação/compilação.
2. Abra `Assets/Scenes/TestPlayground.unity`, se já existir. A cena deve ser aberta sozinha para que câmeras/luzes de outras cenas não interfiram.
3. Para gerar ou reconstruir, execute **Tools > Test Playground > Criar ou reconstruir cenario**. O arquivo será salvo em `Assets/Scenes/TestPlayground.unity` e as cenas que estavam abertas serão preservadas.
4. Execute **Tools > Test Playground > Abrir cena** para abrir o resultado sozinho. A Unity poderá solicitar que você salve alterações pendentes nas cenas abertas, evitando perda de trabalho.

Se houver uma cena sem nome aberta, a Unity solicitará que ela seja salva antes de gerar outra cena em paralelo. Cancelar preserva a cena sem nome e interrompe a geração.

O código fica exclusivamente em `Editor/`. Nenhum gerador, controlador, Rigidbody ou script de execução é adicionado à cena. Os materiais, a textura da grade e as malhas são gerados localmente em `Assets/TestPlayground/Generated/`; não há downloads ou assets pagos.

A reconstrução substitui somente a raiz reservada `TestPlayground_Generated`, reutiliza os mesmos caminhos de assets e salva novamente a mesma cena: não acumula objetos. Alterações manuais **dentro dessa raiz** serão refeitas. Para preservar seus próprios objetos, mantenha-os em outras raízes da cena. Os recursos em `Generated/` também são atualizados; duplique um material se quiser personalizá-lo independentemente. Uma cena preexistente com o mesmo caminho, mas sem a raiz reservada, não é sobrescrita.

## Organização e medidas

O chão ocupa X/Z entre −40 e +40 m, com superfície em Y = 0. A grade é uma textura de 5 × 5 m, repetida 16 vezes: linhas a cada 1 m e linhas mais fortes a cada 5 m. Pinturas no piso e legendas não têm colisores.

| Raiz de setor | Conteúdo |
| --- | --- |
| `00_Ground_and_CentralArea` | Chão 80 × 80 m; área central livre 16 × 16 m; `PlayerSpawn` vazio em (0, 0.05, 0). |
| `01_Ramps_15_30_45deg` | Noroeste: rampas de 4 m de largura, com trechos horizontais de 10, 8 e 6 m e inclinações de 15°, 30° e 45°; patamares apoiados no chão. |
| `02_Platforms_Connected` | Nordeste: plataformas a 1.5, 3 e 4.5 m, conectadas por rampas com fundações; blocos isolados a 0.75 e 2.25 m. |
| `03_Stairs_VariedSteps` | Sudoeste: degraus com altura/profundidade de 0.15/0.40 m, 0.25/0.60 m e 0.40/0.80 m; largura de 4 m. |
| `04_Collisions_Boxes_Walls_Pillars` | Oeste: cubos de 1, 2 e 3 m; muros de 1 e 2 m de altura; pilares cilíndricos de 2, 3.5 e 5 m. |
| `05_Corridors_and_Clearances` | Leste: corredores de 12 m com larguras livres de 0.9, 1.4 e 2.2 m; travessas com altura livre de 1.4, 2 e 3 m. |
| `06_Gaps_0.5_1_1.5m` | Sul: três pistas com plataformas a 1 m e vãos de 0.5, 1 e 1.5 m, medidos entre as bordas; rampas de entrada. O chão continua sob os vãos. |
| `07_Flight_Arches_and_Pillars` | Sudeste: três arcos com larguras internas de 4, 5 e 6 m e ápices internos de 4, 5.5 e 7 m; pilares adicionais de 2.5, 4 e 6 m. |
| `08_Lighting_and_Overview` | Câmera ortográfica de visão geral, luz principal com sombras e luz de preenchimento. |

Há faixas de circulação entre os setores e a área central. Rampas são prismas triangulares sólidos: a entrada encontra o chão sem a borda vertical que surgiria ao inclinar um cubo. Plataformas e degraus são blocos apoiados; rampas elevadas têm fundações. Os únicos elementos suspensos são travessas e coroas dos arcos, conectadas aos seus apoios.

Cubos usam `BoxCollider`. Rampas e cilindros usam `MeshCollider` convexo; cilindros têm tampas planas, sem a cápsula arredondada do primitivo padrão. Os arcos usam `MeshCollider` estático não convexo para preservar a abertura. Todos os sólidos são estáticos e não têm Rigidbody.

## Validação disponível

**Tools > Test Playground > Validar cena salva e repeticao** gera, salva, abre novamente e reconstrói a cena duas vezes. A operação substitui a raiz gerada, como o menu de criação. Ela verifica:

- caminhos únicos na hierarquia; equivalência dos nomes, transforms, componentes e referências de materiais após a reconstrução;
- dimensões e altura do chão, parâmetros da grade e área central livre;
- `PlayerSpawn` vazio; ausência de Rigidbody e de scripts de runtime na raiz gerada;
- colisores sólidos estáticos em todos os obstáculos e shaders disponíveis;
- inclinações das três rampas medidas por raycast no colisor e bases em Y = 0;
- tamanhos dos vãos medidos entre os colisores das plataformas;
- aberturas dos arcos desimpedidas e coroas sólidas, verificadas por raycast;
- interpenetrações entre sólidos por `Physics.ComputePenetration`, com tolerância de 2 mm para contatos de construção;
- sólidos dentro do perímetro do chão e uma câmera de visão geral.

O relatório fica em `Logs/TestPlayground-validation.txt`. Quando há dispositivo gráfico, a ferramenta também tenta salvar `Logs/TestPlayground-overview.png`. Esses arquivos só comprovam uma execução quando existem e indicam sucesso; a presença do código de validação não significa que ele foi executado.

Na entrega, a cena foi efetivamente gerada e salva pela Unity 6000.6.7f1. A execução final em modo batch utilizou uma cópia temporária com as configurações e os pacotes locais do projeto, terminou com código 0 e aprovou 622 verificações geométricas por geração, além de reabrir a cena salva e comparar duas reconstruções. O resultado contém 143 objetos e 85 colisores. A prévia foi renderizada pela Unity e inspecionada quanto à distribuição, à grade e às cores dos setores. Os arquivos gerados, o relatório e a prévia foram trazidos para este projeto; a cena temporária não é necessária para abrir o resultado.

Para automação em uma instância da Unity com o projeto fechado em outros Editors:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe' `
  -batchmode -projectPath 'C:\Users\Usuario\flappy bird x war thunder 001' `
  -executeMethod TestPlayground.Editor.TestPlaygroundBuilder.GenerateAndVerify `
  -quit -logFile 'Logs/TestPlayground-batch.log'
```

O gerador também reconhece um pedido local explícito em `Temp/TestPlayground.Generate`, somente após a compilação/importação de seu código. Ele consome esse arquivo uma única vez e executa a mesma validação. Importar o projeto normalmente, sem esse arquivo, não gera cenas automaticamente.

## Verificação manual ainda necessária

Inspecione a cena na Scene View e Game View, incluindo sombras, grade, legendas e enquadramento na proporção de tela desejada. Ao mudar de nível de qualidade, confira a iluminação com os assets URP PC/Mobile existentes.

Quando houver um controlador, teste em Play Mode: contato no início/fim das rampas e nas conexões; limite de inclinação; altura máxima de degrau; saltos e vãos; largura dos corredores e altura das passagens; colisões laterais e voo através dos arcos. Valores como `slopeLimit`, `stepOffset`, raio e altura do personagem determinarão quais obstáculos serão transitáveis. Esta etapa não cria nem configura um controlador.

Os testes geométricos de Editor não substituem a simulação de Play Mode. Não foram implementados testes de movimentação ou de voo.
