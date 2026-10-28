# Origem preservada

`modelo.blend` é uma cópia binária de `C:\Users\Usuario\Documents\Projeto_Blender\passaro_low_poly.blend`, caminho confirmado pelo autor. Ambos tinham SHA-256 `A148AA3EA12736F7D8EF3E8CD816367EBB465BFFF2E91A90582B465A45E4EF72` na importação.

Nenhum dos originais é salvo pelos scripts de inspeção/exportação. A exportação mantém `Personagem_Root`, as 57 malhas, os marcadores, os nomes e as UVs. Não há armature; existem 184 blend shapes de recolhimento. Câmeras, piso, luzes e imagens de apresentação são excluídos do FBX.

O `AGENTS.md` do projeto de modelagem foi lido. Não havia `AGENTS.md` na pasta deste projeto Unity ou nos seus ancestrais verificados. A entrega acrescenta testes permanentes de input, estado de voo, importação e integração com física real.

As UVs têm sobreposição e não são um atlas exclusivo. As penas são peças separadas com encaixes intencionais: um futuro rig exigirá planejar pesos/parentes e avaliar a deformação nas raízes. Não unir automaticamente as peças. Os drivers de shape keys do Blender não são executados pela Unity; os pesos continuam disponíveis no FBX. Não há animação de asas pronta, nem bake de animação ou root motion.

A orientação original é +X para o bico e +Z para cima, com origem próxima do chão. O FBX mantém a escala de autoria. Somente o prefab aplica escala visual de 0,18 e centraliza a aparência no corpo físico, verificando que o bico aponta para +Z na Unity. Medidas resultantes aproximadas: altura 0,90 m, comprimento 1,14 m, envergadura 1,54 m.

As três imagens extraídas em `Assets/Art/FlappyBird/Textures/References` são referências empacotadas. Nenhuma é textura de superfície do personagem. Os oito materiais usam cores e rugosidade uniformes do Principled BSDF; não há redes procedurais a reproduzir nem mapas externos faltando. URP Lit aproxima esses parâmetros, com diferenças esperadas de luz, exposição e gerenciamento de cor em relação ao Blender.

Para inspecionar/exportar novamente, a partir da raiz do projeto:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools\FlappyBird\inspect_model.py
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python Tools\FlappyBird\export_model.py
```

O segundo comando também reimporta o FBX no Blender e verifica nomes, contagem de triângulos/UVs/blend shapes, fechamento, orientação externa e preservação do original. Se modificadores forem acrescentados no futuro, o exportador interrompe para revisão explícita, pois aplicá-los indiscriminadamente pode destruir blend shapes. O FBX tem caminho fixo para manter sua identidade na Unity; preserve seus `.meta` nas próximas reimportações. Depois execute o menu de configuração de voo na Unity.
