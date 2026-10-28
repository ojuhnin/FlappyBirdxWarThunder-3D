# Validação executada

## Ambiente e escopo

- Blender 5.2.3 LTS, build `e13b728c6880`.
- Unity 6000.6.7f1, URP 17.6.0, Input System 1.20.1, Test Framework 1.8.0 já disponível no projeto.
- Não havia `AGENTS.md` no projeto Unity ou nos ancestrais verificados. Foi lido o `AGENTS.md` do projeto Blender confirmado pelo autor, incluindo a exigência de testes permanentes e verificação antes/depois de correções.
- Unity executada em uma cópia temporária dos Assets, Packages e ProjectSettings, com o cache local de pacotes. A execução batch exigiu acesso ao licenciamento fora da restrição. Não foram instalados pacotes nem modificados o pipeline ou o Input System existente.

## Modelo

Executados `Tools/FlappyBird/inspect_model.py` e `export_model.py` pelo Blender em background, com carregamento sem executar scripts incorporados e sem salvar o `.blend`.

Resultados: 57 malhas, 11.504 triângulos, oito materiais, nenhum armature/modificador e 184 blend shapes. A reimportação real do FBX pelo Blender preservou nomes, contagem de triângulos, camadas UV e blend shapes, fechamento e orientação externa das malhas. O original externo e a cópia `SourceArt/FlappyBird/modelo.blend` mantiveram SHA-256 `A148AA3EA12736F7D8EF3E8CD816367EBB465BFFF2E91A90582B465A45E4EF72`.

As três imagens empacotadas foram extraídas como referências. Nenhuma é mapa de superfície dos oito materiais; não houve baking de texturas. A auditoria dos nós confirmou somente Principled BSDF e Material Output, com entradas não ligadas a redes procedurais.

Na Unity, um teste permanente conferiu 57 renderers, UVs/normais presentes, 11.504 triângulos, 184 blend shapes e oito materiais externos URP Lit. A montagem conferiu frente +Z, altura aproximada de 0,90 m e envergadura inferior a 1,65 m.

Relatórios: `Logs/FlappyBird-model-inspection.json` e `Logs/FlappyBird-export-validation.json`.

## Integração e testes

`FlappyFlight.Editor.FlightSetup.BuildAndValidate` foi executado, terminou com código 0, configurou duas vezes e reabriu a cena salva. Conferiu raiz de integração única, um jogador, uma câmera/HUD/AudioListener ativos, spawn aéreo desobstruído, física fora da hierarquia visual, materiais URP e assinatura dos colisores do playground inalterada.

Após copiar a entrega para o projeto principal, 75 arquivos foram comparados por SHA-256 com a versão validada, sem divergências. Todas as referências GUID da entrega resolveram para assets locais ou dos pacotes instalados, sem GUIDs duplicados. Dos 124 arquivos originais conferidos, somente `Assets/Scenes/TestPlayground.unity` foi alterado para receber a integração; os outros 123 permaneceram idênticos. A cena anterior está preservada em `Logs/FlappyFlight-backups/TestPlayground.before-flight.unity`. As configurações de pipeline, pacotes e input original não foram alteradas.

| Suíte | Resultado final |
| --- | --- |
| EditMode — `FlappyFlight.EditModeTests` | **23 aprovados, 0 falhas, 0 ignorados** |
| PlayMode — `FlappyFlight.PlayModeTests` | **11 aprovados, 0 falhas, 0 ignorados** |
| Total | **34 testes aprovados** |

EditMode cobre direção desejada independente, taxas limitadas, prioridade de pitch/yaw manual, retomada sem alvo antigo, estabilização de roll, olhar livre com teclado, velocidade sempre positiva, reinício, comparação numérica de passos 30/60/144 Hz, bindings reais individuais/combinados, importação e estrutura do prefab.

PlayMode instancia os prefabs reais e executa Rigidbody/colisões: avanço contínuo no ar, rotação física correspondente ao controlador, contato com chão/muro seguido de reinício, tecla R pelo Player Loop real, combinação W/D/Q/Shift/C, restauração da câmera, retorno suave do olhar livre, estado do retículo e sphere cast de obstrução.

Os testes de input usam o fixture oficial do Input System para isolar dispositivos/foco e restaurar o estado após cada caso. Uma primeira tentativa sem esse fixture não recebia eventos de teclado em EditMode; isso era uma limitação da configuração do teste, corrigida antes da avaliação funcional.

### Regressão demonstrada antes e depois

`RealPlayerLoopReceivesCombinedKeyboardDuringFreeLook` falhou com a versão que aplicava `Application.isFocused` novamente sobre comandos já filtrados pelo Input System. Entradas válidas do runtime isolado chegavam às ações, mas pitch permanecia em 0. O relatório anterior mostra **7 aprovados e 1 falha**, com expectativa de pitch maior que 3 e resultado 0.

A correção removeu esse segundo filtro, mantendo limpeza dos comandos e liberação do cursor ao perder foco. O mesmo teste passou, sem afrouxar a expectativa. Foi mantido na suíte. A prova anterior fica em `Logs/FlappyFlight-playmode-before-fix.xml`; o resultado final está em `Logs/FlappyFlight-playmode.xml`.

A injeção de R foi ajustada para a próxima atualização Dynamic real, em vez de consumir o evento de borda antes de `Update`; era uma correção do procedimento de teste, sem mudança do binding de produção.

### Taxas de atualização observadas

Além dos testes numéricos, o Player Loop e a física reais rodaram com os limites abaixo solicitados, vSync desativado temporariamente e restaurado. Durante cada amostra o pássaro continuou voando/virando com velocidade, altura e inclinação limitadas e câmera sem roll.

| Solicitado | Observado | Quadros na amostra | Maior intervalo observado |
| --- | --- | --- | --- |
| 30 Hz | 30,0 FPS | 37 | 33,4 ms |
| 60 Hz | 60,0 FPS | 73 | 17,6 ms |
| 144 Hz | 144,4 FPS | 174 | 8,0 ms |

São amostras curtas em Editor batch, aproximadamente 1,2 s por taxa. Não são benchmark nem medição de latência de dispositivos ou de sensação de jogo. Dados: `Logs/FlappyFlight-render-cadences.txt`.

## Conferência visual e limites

Foram renderizadas pela Unity e examinadas duas imagens estáticas: vista de jogo atrás do pássaro e vista frontal em perspectiva. Elas permitiram conferir importação, cores, silhueta, olhos/bico, escala e enquadramento inicial com o playground. Arquivos: `Logs/FlappyFlight-camera-preview.png` e `Logs/FlappyFlight-model-preview.png`.

Não houve sessão humana de pilotagem nem observação contínua do jogo. O estado/projeção do HUD foi verificado por testes; sua legibilidade visual em Game View continua pendente. A tentativa opcional de `ScreenCapture` em batch não produziu uma imagem do HUD, portanto não é apresentada como inspeção visual dele.

Não foram medidos desempenho sustentado, consumo de GPU, latência de mouse, conforto ou fidelidade do controle ao War Thunder. O voo é uma adaptação assistida sem aerodinâmica completa. Colisores são aproximações fixas do corpo e das asas abertas; não se deformam com futuros ossos/blend shapes.

## Procedimento manual pendente

1. Abrir `TestPlayground.unity` sozinha, Play, clicar na Game View e confirmar anel branco central/ponto âmbar.
2. Mover o mouse e observar o corpo convergir com atraso. Testar W/S, A/D, Q/E individualmente e combinados, depois soltar e conferir ausência de retorno brusco ao alvo antigo.
3. Aumentar/reduzir velocidade; confirmar que continua avançando no mínimo.
4. Segurar C, olhar para os lados, continuar manobrando por teclado e soltar C; conferir mira sincronizada e retorno confortável da câmera.
5. Usar R em curva/olhar livre e colidir com obstáculos; conferir retomada no ar sem ficar parado no chão.
6. Voar pelos arcos, perto de muros e em cantos, avaliando os volumes de colisão, obstrução e ocultação da aparência quando a câmera se aproxima.
7. Avaliar sensibilidade/FOV, HUD e proporções de tela com mouse/teclado reais. Ajustar `PrototypeFlight.asset` e `FlightChaseCamera` conforme o conforto pretendido.

Comandos reproduzíveis de Unity estão em `README.md`; comandos Blender e limites de rig em `SourceArt/FlappyBird/README.md`.
