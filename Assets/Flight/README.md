# Flappy Flight — protótipo de voo assistido

## Testar

Abra `Assets/Scenes/TestPlayground.unity` sozinha e pressione **Play**. O pássaro começa no ar, em `(0, 5, -4)`, já a 5,5 m/s. Clique na Game View se o cursor não estiver capturado. Não há decolagem, pouso ou animação de asas.

Para montar/repetir a integração: **Tools > Flappy Flight > Configurar jogador no TestPlayground**. Depois, **Tools > Flappy Flight > Abrir teste de voo**. A configuração salva a cena, substitui somente `FlappyFlight_Integration` e reutiliza os caminhos de prefab/assets. Ela não reconstrói os obstáculos. A antiga `OverviewCamera` e seu AudioListener ficam desativados, disponíveis para inspeção. Se reconstruir o greybox pelo menu anterior, execute a configuração de voo novamente para desativar essa câmera.

O menu reconstrói os dois prefabs e a raiz de integração. Mantenha objetos próprios fora dessa raiz. Os valores do asset `PrototypeFlight.asset` são preservados entre configurações; alterações manuais na instância gerada ou nos prefabs devem ser copiadas antes de reconstruí-los. A Unity pede para salvar uma cena sem nome antes de abrir o playground em paralelo.

## Controles

| Entrada | Ação |
| --- | --- |
| Mouse | Direção desejada independente do corpo; mover para cima levanta o nariz. |
| W / S | Nariz para baixo / para cima. |
| A / D | Rolar para esquerda / direita; a inclinação também induz curva suave. |
| Q / E | Guinada para esquerda / direita. |
| Shift / Ctrl | Aumentar / diminuir progressivamente a velocidade desejada; o valor permanece ao soltar. |
| C + mouse | Olhar livre; o mouse não muda a intenção de voo, e o teclado continua funcionando. |
| R | Reiniciar no spawn aéreo, restaurando orientação, velocidade, assistência e câmera. |
| Esc | Liberar cursor; o voo continua. |
| Clique esquerdo | Recapturar o mouse; o primeiro delta é descartado. |

Anel branco central: direção desejada, paralela à câmera normal. Ponto âmbar: direção efetiva do nariz, projetada à frente do pássaro, sem balística. O anel e o ponto somem no olhar livre e durante a transição de retorno, quando o centro da câmera ainda não coincide com a intenção. O texto informa esse estado.

Colidir com chão/obstáculo solicita reinício na próxima etapa física, evitando permanecer no chão. Sair dos limites de segurança (`|X|` ou `|Z| > 55 m`, altura fora de −3 a 45 m) também reinicia o teste. Não há navegação automática para evitar obstáculos.

## Hierarquia e separação

- `FlappyFlight_Integration/FlightSpawn_Airborne`: ponto aéreo seguro, separado de `PlayerSpawn` do greybox.
- `FlappyBirdPlayer`: Rigidbody dinâmico, CCD, dois colisores de aproximação e controlador na raiz.
- `VisualRoot_FutureRig`: apenas aparência, com instância do FBX e suas 57 peças; escala visual 0,18.
- `CameraTarget_Stable` e `AimOrigin_Stable`: filhos diretos da raiz física, independentes de asas, shape keys e futuros ossos.
- `FlightCameraRig`: outra raiz de prefab, sem herdar rolagem do pássaro; contém câmera, acompanhamento e HUD.

A física não depende de ossos. Uma futura armature/Animator deve ficar sob `VisualRoot_FutureRig`, com **Apply Root Motion desativado** e animações sem mover a raiz física. O arquivo atual não tem armature: a importação usa Animation Type **None**, mantendo os blend shapes. Quando houver esqueleto, revise o exportador e mude para **Generic**, validando novamente a hierarquia e os nomes. O driver de recolhimento do Blender não é um controlador de animação na Unity.

O capsule central e a caixa das asas são aproximações fixas da pose aberta. Não acompanham penas ou blend shapes. Um futuro rig exigirá revisar esses volumes; colisão exata de cada pena não faz parte desta versão.

## Parâmetros ajustáveis

`Assets/Flight/Settings/PrototypeFlight.asset`:

| Grupo | Valores iniciais / efeito |
| --- | --- |
| Velocidade mínima / cruzeiro / máxima | 3 / 5,5 / 10 m/s. |
| Variação do alvo / aceleração / desaceleração | 2,5 / 3 / 4 m/s². |
| Taxa máxima de pitch / yaw / roll | 55 / 65 / 110 graus/s. |
| Pitch / bank manual / bank assistido | ±65° / ±75° / ±50°. |
| Assistência | 0–1; intensidade de convergência e nivelamento. |
| Direction Gain / Bank Gain / Rate Response | Convergência da direção, nivelamento e resposta das taxas. |
| Handover Seconds | Retomada gradual após o olhar livre, inicialmente 0,35 s. |
| Degrees Per Pixel / Invert Mouse Y | Sensibilidade inicial 0,12 grau/pixel; inversão opcional. |

No componente `FlightChaseCamera`: distância (4,2 m), altura (0,85 m), FOV (68°), suavização de posição/retorno, raio da esfera de obstrução, margem e máscara de colisão. O horizonte não acompanha a rolagem. A câmera se aproxima ao encontrar obstáculos; a aparência é ocultada se ficar perto demais, evitando cortes pelo corpo. Ajustes extremos e passagens muito apertadas ainda precisam de inspeção manual.

Os bindings ficam em `Assets/Flight/Input/FlappyFlight.inputactions`, separados de `Assets/InputSystem_Actions.inputactions`, que não foi alterado. Cada jogador instancia sua própria cópia das ações. O Input System gerencia o foco/dispositivos; não há API de input legado.

## Assistência e prioridade

`FlightState` mantém orientação física e intenção separadas. O mouse altera somente a intenção; taxas de rotação filtradas limitam a convergência. Comando manual substitui a assistência no eixo correspondente. A rolagem manual também assume a direção horizontal, para que a mira antiga não desfaça a curva.

Ao soltar pitch/yaw/roll manual, a intenção desse eixo é recalculada a partir do movimento atual, com espaço para desaceleração da rotação. Ao sair de C, pitch/yaw desejados são sincronizados com o corpo e a assistência volta progressivamente. A câmera retorna separadamente, sem aplicar sua rotação ao Rigidbody.

É um modelo de voo de protótipo: velocidades e limites são nossos, sem aerodinâmica, sustentação, estol, simulação de motor, armas ou parâmetros de aviões reais.

## Referência pesquisada

Confirmado na explicação publicada na wiki oficial: Mouse Aim indica uma direção; o Instructor transforma essa intenção em comandos dentro dos limites do modelo; teclas podem atuar em pitch, roll e yaw; a assistência também estabiliza o voo. [How the Instructor Works](https://old-wiki.warthunder.com/Instructor/How_the_instructor_works).

Confirmado na referência oficial do CDK: há ações distintas `External View` e `Mouse look activation`. Ela não fornece a distância, o FOV ou a suavização usados pelo jogo. [CDK Reference](https://wiki.warthunder.com/cdk/7439-cdk-reference).

Adaptações desta entrega: prioridades por eixo, sincronização ao soltar teclas/C, horizonte sem roll, retículo central, valores de velocidade/curva/câmera e reinício automático. Não são apresentados como parâmetros ou reprodução exata do War Thunder. O mapeamento de teclas foi escolhido para este protótipo pelo autor.

## Modelo e materiais

Modelos, materiais e imagens estão separados em `Assets/Art/FlappyBird`. Os nomes e os caminhos fixos mantêm os remaps de materiais; preserve os `.meta` em futuras reimportações. O menu atualiza as cores a partir de `Materials/SourcePalette.json`, exportado do Blender.

O original foi copiado e preservado em `SourceArt/FlappyBird/modelo.blend`; o original externo também foi preservado. Leia `SourceArt/FlappyBird/README.md` para limites de rig, UVs, drivers e comandos de exportação. As oito cores/rugosidades do Principled são aproximadas em URP Lit; as três imagens em `Textures/References` são referências e não mapas do personagem. Não havia materiais procedurais complexos nem texturas de superfície faltando.

## Testes reproduzíveis

Na Unity: **Window > General > Test Runner**. Rode `FlappyFlight.EditModeTests` na aba EditMode e `FlappyFlight.PlayModeTests` na aba PlayMode. Os testes instanciam os prefabs reais, usam o fixture oficial de Input System, executam Rigidbody/colisões e restauram o estado global usado por eles.

Pela linha de comando, com o projeto fechado em outros Editors ou usando uma cópia temporária:

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe'
$project = 'C:\Users\Usuario\flappy bird x war thunder 001'
& $unityExe -batchmode -projectPath $project -executeMethod FlappyFlight.Editor.FlightSetup.BuildAndValidate -quit -logFile Logs/FlappyFlight-build.log
& $unityExe -batchmode -projectPath $project -runTests -testPlatform EditMode -assemblyNames FlappyFlight.EditModeTests -testResults Logs/FlappyFlight-editmode.xml -logFile Logs/FlappyFlight-editmode.log
& $unityExe -batchmode -projectPath $project -runTests -testPlatform PlayMode -assemblyNames FlappyFlight.PlayModeTests -testResults Logs/FlappyFlight-playmode.xml -logFile Logs/FlappyFlight-playmode.log
```

`BuildAndValidate` configura duas vezes, reabre a cena salva e verifica jogador/câmera/HUD/AudioListener únicos, spawn livre, materiais e preservação geométrica do terreno. Os scripts Blender inspecionam o original e fazem exportação/reimportação real de FBX. Resultados efetivamente executados constam em `VALIDATION.md`, sem substituir a inspeção manual.

Ainda é necessário jogar com mouse e teclado reais na Game View para avaliar conforto, sensibilidade, colisores em todas as passagens, oclusão em cantos, legibilidade do HUD e proporções de tela diferentes. Há comparações numéricas com passos de 30/60/144 Hz e uma execução curta do Player Loop real solicitando essas taxas; os FPS efetivamente observados ficam no relatório. Essas amostras em batch não são benchmark nem validação da latência do mouse ou do conforto de jogo.
