# Glock VR — Plano de desmontagem

Treinamento de desmontagem de uma Glock 17 Gen5 em VR. Projeto de portfólio.

## Decisões já tomadas

- **Alvo:** Meta Quest 2 e 3 (Android). Base de interação: **Meta Interaction SDK** + Meta XR Core SDK.
- **Modelo:** low poly da coleção `Glock_LP` (`glock.blend`), exportado em `Assets/_Project/Arts/Models/Glock/glock.fbx`.
  - Scale Factor no import: **0.113568** (arma com ~20 cm reais).
  - Import Tangents: **Import** (tangentes do Blender, para o normal map bater com o bake).
- **Materiais (URP Lit):**
  - `Glock_Main`: atlas 2048 (frame, ferrolho, cano e peças pequenas).
  - `Glock_Main_Spring`: mesmo atlas, com alpha clip e duas faces.
  - `Glock_Mag`: atlas 1024.
  - Texturas de cada atlas: `_BaseColor`, `_Normal`, `_Mask` (R metallic, G AO, A smoothness) e `_Emission`. Override de Android em ASTC.
- **Peças separadas:** cada peça é um objeto próprio, porque o app é de desmontagem.
- **Ordem do trabalho:** primeiro a mecânica da arma funcionando sozinha (fases C e D). Depois o sistema de etapas que valida a ordem (E). A montagem é o inverso (F). Hand tracking só depois que o controle estiver redondo (G).

## Etapas do treinamento (fase E)

1. Remover o carregador.
2. Puxar o ferrolho para trás 3 vezes.
3. Inspeção visual da câmara.
4. Inspeção física: encostar o indicador (ou o controle) na câmara.
5. Apontar para local seguro e fazer o disparo a seco.
6. Empunhadura de desmontagem: recuar o ferrolho, puxar a alavanca e soltar o ferrolho para frente pelos trilhos.
7. No conjunto do ferrolho, tirar o conjunto da mola recuperadora.
8. No conjunto do ferrolho, tirar o cano.

A montagem segue a ordem inversa. Para encaixar o ferrolho não é preciso puxar a alavanca.

## Interações

### Controle

| Onde pega | O que acontece |
|---|---|
| Meio do cabo | Empunhadura de arma (snap). O gatilho do controle aciona o gatilho da arma. |
| Entre o cabo e o ferrolho, em ângulo específico | Empunhadura de desmontagem: o gatilho analógico (0–1) recua o ferrolho até o ponto de soltar a alavanca. |
| No ferrolho | Se a arma está solta, pega a arma inteira (sem snap). Se a outra mão segura a arma, o ferrolho corre no trilho. |
| Na alavanca | Grip + gatilho analógico puxa a alavanca para baixo, proporcionalmente. |
| Resto do modelo | Pega mantendo a posição atual, sem snap. |

### Hand tracking (fase G)

| Onde pega | O que acontece |
|---|---|
| Meio do cabo | Gesto de empunhadura (indicador esticado, demais dedos fechados): snap no cabo. |
| Entre o cabo e o ferrolho | Fechar os dedos ou mover o polegar recua o ferrolho. |
| No ferrolho | Mesma regra do controle. |
| Na alavanca | Polegar e indicador encostando nas duas pontas abaixam a alavanca. |
| Resto do modelo | Pega sem snap. |

Usar histerese em tudo: as mãos se sobrepõem e o rastreio perde dedos.

### Mecânica

- **Ferrolho sem a alavanca abaixada:** recua até o limite e volta pela mola ao soltar.
- **Ferrolho recuado + alavanca abaixada:** fica livre para sair pela frente.
- **Cano:** movimento calculado a partir do curso do ferrolho (0–1), sem física.
  - Primeiros ~3 mm: recua junto com o ferrolho.
  - Depois: a culatra desce e a boca sobe levemente, girando em torno de um pivô.
  - Por fim: o cano para e o ferrolho segue.
- **Conjunto da mola recuperadora:** quatro peças.
  - `guide_rod_recoil_spring_LP`: guia, fica parada.
  - `outer_sleeve_LP`: luva externa, acompanha o ferrolho.
  - `outer_recoil_spring_LP`: mola externa, acompanha a luva. A origem fica na ponta traseira, caso precise comprimir.
  - `inner_recoil_spring_LP`: mola interna, um tubo só da flange da guia até a frente da luva. A origem fica na ponta traseira, e ela comprime com escala no eixo, acompanhando a luva.
- **Hierarquia muda com o estado:**
  - Montada: cano e mola seguem o `BarrelDriver` do frame.
  - Ferrolho solto: cano e mola viram filhos do ferrolho.
  - Depois: cada peça pode sair como objeto livre.
- **Retém do ferrolho (`slide_release`):** precisa travar o ferrolho aberto, para a inspeção visual e física.

Hierarquia do prefab `Assets/_Project/Prefabs/Glock.prefab`:

```
Glock (frame, segurável)
├─ Frame → g17_gen5_LP
├─ TriggerPivot → trigger_LP, TriggerSafetyPivot → trigger_safety_LP
├─ TakedownLeverPivot → dissembly_lever_LP
├─ SlideStopPivot → slide_release_LP
├─ MagReleasePivot → magazine_release_LP
├─ Magazine → mag_empty_LP, mag_follower_LP
├─ SlideGroup (trilho com limites) → slide_LP, extractor_LP, barrel_cover_LP
├─ BarrelDriver (pose calculada pelo curso do ferrolho) → barrel_LP
├─ RecoilSpringAssembly → guide_rod_recoil_spring_LP
│   ├─ OuterSleeve → outer_sleeve_LP, outer_recoil_spring_LP (acompanha o ferrolho)
│   └─ InnerSpring → inner_recoil_spring_LP (escala em Z comprime)
├─ PhysicsColliders (C1: colliders do corpo; o do ferrolho fica em SlideGroup/SlideCollider)
└─ GripZones (C1: Zone_Handle, Zone_Takedown, Zone_SlideFree, Zone_Lever, Zone_Free;
              Zone_SlideDrag fica dentro do SlideGroup)
```

- **Nós de pivô:** cada nó `*Pivot` e de grupo está na origem da peça e com rotação zerada. Os scripts mexem nesses nós, nunca nas malhas, porque as malhas vêm do FBX com rotação de 270° e escala 100.
- **Eixos na Unity:** a boca do cano aponta para **+Z**, então o ferrolho recua em −Z. O lado +X do Blender vira −X na Unity.
- **Carregador:** sai inclinado, pelo eixo do cabo, não em Y puro. Ajustar isso na D4.
- **Conjunto da mola:** fica no topo da hierarquia, não dentro do cano. Na D7 ele vira filho do ferrolho, junto com o cano.
- **FBX desempacotado no prefab:** as malhas continuam vindo do `glock.fbx` e atualizam a cada reexport. Já as posições dos nós não atualizam sozinhas: se um pivô mudar no Blender, ajustar o nó correspondente.

## Tarefas

Uma de cada vez. Cada tarefa só termina quando o item "Pronto quando" estiver conferido.

### A — Preparar o modelo

- [x] **A1. Conferir os pivôs.** Comparar a origem de cada peça `_LP` com o ponto em que ela gira ou desliza (pino do gatilho, eixo da alavanca, retém do ferrolho, trilho do ferrolho, eixo do carregador).
  *Pronto quando:* há uma lista do que está certo e do que precisa mudar.
- [x] **A2. Corrigir os pivôs e reexportar.** Ajustar as origens no Blender sem mover a geometria e reexportar o FBX.
  *Pronto quando:* na Unity, cada peça gira ou desliza no lugar certo ao mexer no Transform.
  - Ajustados: `trigger_LP` no pino do gatilho (Blender 0, 0.0732, 0.9106), `recoil_spring_LP` na ponta traseira (a escala no eixo comprime para frente) e `slide_LP` centralizado em X.
  - Sem mudança: cano (o pivô da inclinação fica no script), carregador, retém do carregador, alavanca, extrator, tampa e frame.
  - Depois o usuário ajustou os pivôs do retém do ferrolho, retém do carregador, alavanca, tampa e trava do gatilho. A trava estava com x = 1,7 (fora da arma) e foi centralizada em x = 0. Os mesmos pivôs foram aplicados no HP.
- [x] **A3. Montar o prefab `Glock`.** Hierarquia acima, a partir do FBX.
  *Pronto quando:* o prefab abre com as peças nos lugares certos e os pais corretos.

### B — Setup XR

- [x] **B1. Instalar os pacotes e configurar para Quest.** Meta XR Core SDK, Interaction SDK, build target Android, OpenXR e URP com configurações mobile (MSAA 4x, sem HDR e sem pós-processamento).
  *Pronto quando:* o Project Validation da Meta fica sem erros.
  - **Pacotes:** Meta XR Core, Interaction SDK Essentials e Interaction SDK 207.0.1, instalados pela Asset Store porque saem assinados. OpenXR 1.18 e XR Hands 1.9.
  - **Loader OpenXR:** Android e PC. O PC serve para testar pelo Quest Link no editor.
  - **Features no Android:** Meta Quest Support, Meta XR, Meta XR Foveation e os perfis Touch, Touch Plus e Touch Pro. Latency Optimization em *Prioritize Input Polling*.
  - **Features no PC:** Meta XR e os mesmos perfis de controle.
  - **Fix All da Meta:** pelo menu `Tools/Glock/Meta: Fix All (Android + PC)`.
  - **URP Mobile:** MSAA 4x, sem HDR, sem depth texture e sem opaque texture, render scale 1 e sombra até 4 m. O nível de qualidade PC fica fora do Android. O SSAO do renderer PC está desligado.
  - **Validação do OpenXR:** sobra só o aviso do SSAO do renderer PC, que não entra no build do Quest.
- [x] **B2. Cena de teste com rig mínimo.** Controles e mãos, com grab e poke, sem locomoção.
  *Pronto quando:* dá para pegar um cubo com o controle no editor (Link) ou no Quest.
  - Cena `Assets/_Project/Scenes/B2_TestRig.unity`, na lista de build. Tem chão, mesa, um cubo pegável e o prefab `Glock`, que ainda não tem interação.
  - **Rig:** `OVRCameraRig` (origem no chão) + `OVRComprehensiveInteractionRig` do Interaction SDK, montados do mesmo jeito que o wizard da Meta. O `Locomotor` está desativado.
  - **Mãos:** `OculusProjectConfig` em *Controllers And Hands*. Mãos sintéticas seguindo o controle (*Conforming To Controller*).
  - **Testado no Quest** com *Build And Run*: grab funcionando com controle e com hand tracking.
  - **Quest Link no Play do editor não entrou em VR.** Suspeitas, por ordem:
    - *Unknown Sources* desligado no app Meta Horizon Link.
    - Runtime OpenXR ativo do Windows não é o do Link.
    - *Play Mode OpenXR Runtime* da aba Windows do OpenXR.
    - Para hand tracking pelo Link: *Developer Runtime Features* no app Link.

### C — Pegar a arma

- [x] **C1. Zonas de pegada no frame.**
  - Cabo: snap.
  - Cabo + ferrolho: desmontagem.
  - Ferrolho.
  - Alavanca.
  - Resto: livre.
  *Pronto quando:* cada zona ativa o modo certo, com prioridade e ângulo de aceitação.
  - **Estado:** concluída e testada no Quest, com controle e hand tracking.
    - Zonas, prioridades, snap e arrasto do ferrolho funcionando.
    - Pegada meio fechada funcionando no hand tracking.
    - A pose do cabo ainda não está 100%; o usuário vai ajustar à mão no editor.
  - **Scripts** em `Assets/_Project/Scripts/Glock/`:
    - `GlockGripZone`: filtro de interactor de cada zona.
    - `GlockGripController`: na raiz. Escolhe a zona vencedora de cada mão e guarda o modo atual (`GetMode`, `WhenModeChanged`).
    - `GlockGripDebugLabel`: texto sobre a arma, só na cena de teste.
  - **Montagem:** pelo menu `Tools/Glock/Montar zonas de pegada (C1)`, script `Assets/_Project/Editor/GlockGripSetup.cs`.
    - Rodar de novo refaz volumes, prioridades e regras.
    - Poses de snap que já existem não são tocadas.
  - **Física:** a raiz tem Rigidbody (0,7 kg) e colliders simples em `PhysicsColliders`. O collider do ferrolho fica dentro do `SlideGroup`. Solta, a arma cai; segurada, fica cinemática.
  - **Grabbables:**
    - Arma: na raiz, com `GrabFreeTransformer`. Aceita um ponto de pegada por vez.
    - Ferrolho: provisório até a D1. `OneGrabTranslateTransformer` só em Z, com 45 mm de curso.
    - Alavanca: provisória, travada.
  - **Zonas:** cada uma é um filho com Rigidbody cinemático, um volume trigger e o(s) `HandGrabInteractable`.
    - Todas pegam com a palma, e basta um entre médio, anelar e mínimo fechar.
    - Polegar e indicador são ignorados: o indicador fica livre para o gatilho, e o gatilho do controle nunca pega a arma.
    - Antes eram exigidos médio e anelar juntos. No hand tracking, isso impedia pegar a arma deitada na mesa com o indicador esticado, porque os dedos não davam a volta no cabo.
    - **Limiares no hand tracking:** o padrão do SDK fecha a pegada com força 0,9 e solta com 0,85.
      - Sem nada contra os dedos, era preciso fechar a mão por completo para pegar, e a arma caía perto do limite.
      - O `PalmGrabTuning` (`Assets/_Project/Scripts/Rig/`), na raiz do `OVRComprehensiveInteractionRig`, troca para início em 0,55 (mão meio fechada) e soltura em 0,35.
      - Vale para todas as pegadas de palma e é ajustável no inspector.
      - Precisa estar no rig de toda cena.
      - Nos controles a pegada segue o botão de grip e não passa por esse limite.
    - O texto de debug mostra a força de médio, anelar e mínimo (`M/A/m`) de cada mão.

    | Zona | Modo | Prioridade | Outra mão | Snap | Pega |
    |---|---|---|---|---|---|
    | `Zone_Lever` | Lever | 50 | segurando a arma | não | alavanca |
    | `Zone_Takedown` | Takedown | 40 | sem segurar | sim; palma para baixo, até 55° | arma |
    | `Zone_SlideFree` | SlideFree | 30 | sem segurar | não | arma |
    | `Zone_SlideDrag` (filho do `SlideGroup`) | SlideDrag | 30 | segurando a arma | não | ferrolho |
    | `Zone_Handle` | Handle | 20 | qualquer | sim | arma |
    | `Zone_Free` | Free | 0 | qualquer | não | arma |

  - **Escolha da zona:** a palma precisa estar dentro do volume, com 1 cm de folga. Vence a maior prioridade e, no empate, a mais próxima.
  - **Regras entre as mãos:**
    - Pegar o corpo da arma com uma mão solta a outra (troca de mão).
    - Soltar o corpo da arma solta também a mão que estava no ferrolho ou na alavanca.
  - **Poses de snap:**
    - Um `HandGrabInteractable` por mão (`HandGrab_Right`/`HandGrab_Left`), porque cada um aceita poses de uma mão só.
    - Só o pulso faz snap; os dedos ficam livres.
    - As poses são aproximadas. Ajustar no editor, selecionando o `Pose` com a mão fantasma.
    - **Ajuste no editor:** mexer só na pose da mão direita, no Prefab Mode da `Glock`. Depois usar `Tools/Glock/Espelhar poses de snap (direita para esquerda)`, que espelha o pulso no plano YZ da arma e as juntas dos dedos, e salvar o prefab.
  - **Debug:** na cena `B2_TestRig`, o texto sobre a arma mostra:
    - o modo de cada mão (E/D);
    - a zona que a mão pegaria agora ("mira");
    - o ângulo da palma em relação à zona de desmontagem.
- [x] **C2. Gatilho.** O gatilho da arma segue o gatilho do controle na empunhadura. Registrar o estado do percussor (armado ou disparado).
  - **Estado:** concluída e testada no Quest.
  - **Polimento (usuário, depois):** um pouco mais de curso no gatilho (`Trigger Angle`). No hand tracking, o indicador não deveria precisar fechar tanto.
    - Faixas separadas: `Input Range` (controle, 0,05–0,95) e `Hand Input Range` (hand tracking, 0,15–0,55).
    - A do hand tracking foi encurtada porque o indicador dava a volta no cabo e acertava o retém do carregador.
  - **Script:** `GlockTrigger`, na raiz da `Glock`.
  - **Entrada:** só no modo Handle. Usa a força do indicador no `HandGrabAPI` da mão que segura.
    - No controle, é o gatilho analógico (o SDK injeta o `ControllerPinchAPI`).
    - No hand tracking, é a curvatura do indicador.
    - A faixa 0,05–0,95 vira curso 0–1.
  - **Curso:**
    - Nos primeiros 15% só a trava (lâmina) afunda, até 10°.
    - Depois o gatilho inteiro gira até 12° no X do `TriggerPivot`. Mais que ~13° a sapata bate no frame.
  - **Percussor:**
    - Solta em 90% do curso (`WhenStrikerReleased`).
    - Depois disso o gatilho fica atrás (gatilho morto) até o percussor ser armado de novo (`Cock()`, `WhenStrikerCocked`).
    - Começa armado.
  - **Reset e armar, como na arma real (ajustado depois da D4):**
    - Depois do disparo o gatilho fica atrás até o ferrolho recuar 1 cm (`Trigger Reset Travel` no `GlockSlide`); aí ele volta para a frente.
    - Os 8 mm da desmontagem não chegam a isso.
    - O percussor só é armado quando o ferrolho volta à bateria.
    - Fora da bateria o gatilho não dispara.
    - Quem manobra segurando o gatilho precisa soltá-lo até o ponto de reset (`Reset Point`, 60% do curso) para disparar de novo.
  - **Debug:** o texto sobre a arma mostra o curso do gatilho e o estado do percussor.

### D — Mecânica da arma (sem validação de etapas)

- [x] **D1. Ferrolho no trilho.** Limite de curso e retorno por mola.
  - **Estado:** concluída e testada no Quest.
  - **Script:** `GlockSlide`, na raiz da `Glock`.
    - É a fonte do curso: limita a posição que o Grabbable do ferrolho aplica e refaz o limite do `OneGrabTranslateTransformer` ao iniciar.
    - Expõe `Travel01` (0 em bateria, 1 no fim) para a D2 (cano), a D3 (retém) e a D5 (desmontagem).
  - **Curso:** 45 mm. Medido no modelo: a culatra (z −2,1 cm) precisa passar do cartucho de cima do carregador (até ~−6,1 cm). A mola vai de 8,3 cm para ~3,8 cm comprimida.
  - **Mola:** solto, o ferrolho acelera para a frente a 25 m/s², uns 60 ms do fim do curso até a bateria.
  - **Manobra:** passar de 80% do curso e voltar à bateria conta uma manobra (`WhenRacked`) e arma o percussor. Isso substitui o armar provisório da C2.
  - **Debug:** o texto sobre a arma mostra o curso do ferrolho e quantas manobras foram feitas.
- [x] **D2. Cano e mola seguindo o curso do ferrolho.** Curvas e pivô.
  - **Estado:** concluída e testada no Quest.
    - Depois o usuário mostrou fotos (Gen5 e 19X) com o ferrolho todo atrás: a boca do cano aponta bem para cima.
    - O modelo passou a seguir a geometria real (abaixo).
  - **Script:** `GlockSlideLinkage`, na raiz. Roda depois do `GlockSlide` e lê o recuo dele em metros (`Offset`).
  - **Cano:**
    - Recua travado com o ferrolho nos primeiros 3 mm.
    - De 3 a 5,5 mm a culatra desce 3,5 mm no bloco de trava (`Breech Drop`) e o capuz sai da janela de ejeção.
    - O cano sempre passa pelo furo da frente do ferrolho (`Slide Front Z`), na altura do eixo, e esse furo recua junto com o ferrolho. Por isso o ângulo cresce com o curso: ~2,2° logo ao destravar e ~3,7° com o ferrolho todo atrás, como nas fotos.
    - Depois para, e só o ferrolho segue.
  - **Mola:** guia parada (fica dentro do canal do frame); luva externa e mola externa acompanham o ferrolho; a mola interna comprime em Z (8,2 cm → 3,7 cm no fim do curso).
  - **Ajustes no inspector:** recuo travado, recuo total, ângulo e pivô do cano, comprimento da mola.
- [x] **D3. Retém do ferrolho.** Travar e destravar o ferrolho aberto.
  - **Estado:** concluída no controle, testada no Quest.
    - "Puxar e soltar" testado e funcionando. Corrigido depois: levantar o retém com o ferrolho todo atrás travava e soltava no mesmo instante; agora o puxar e soltar só vale depois que o ferrolho encostou no retém, e com o retém solto.
    - Hand tracking adiantado: encostar a ponta do polegar ou do indicador no retém (qualquer lado, caixa `Touch Box`) funciona como o B/Y. Só vale com a arma na mão. Falta testar.
  - **Script:** `GlockSlideStop`, na raiz. O `GlockSlide` respeita a trava: solto, volta só até o entalhe e, arrastado, não passa dele para a frente.
  - **Trava:** em 93% do curso (`Lock Travel`), só vindo da frente.
  - **Controle:** B/Y numa mão que esteja pegando a arma ou o ferrolho.
    - Segurar B/Y levanta o retém; o ferrolho que chega à trava fica aberto.
    - Com o ferrolho travado, B/Y abaixa o retém e o ferrolho fecha. Isso conta manobra e arma o percussor.
  - **Puxar e soltar:** com o ferrolho travado, puxar até 98% deixa o retém cair. Não vale com carregador vazio inserido, porque o transportador segura o retém.
  - **Carregador vazio:** inserido, trava o ferrolho aberto sozinho (`Empty Magazine Inserted`, começa ligado). A D4 vai controlar isso ao tirar e pôr o carregador.
  - **Retém visível:** o pivô fica na frente e a aba atrás; a aba sobe 4° (~2,6 mm).
  - **Hand tracking:** ainda sem acionamento (fase G: polegar encostando no retém). Com carregador dentro, um ferrolho travado só fecha pelo controle até a D4.
- [x] **D4. Carregador removível.** Retém do carregador e encaixe de volta.
  - **Estado:** concluída e testada no Quest com controle. O encaixe automático passou para 55% a pedido do usuário. Falta testar o retém no hand tracking.
  - **Script:** `GlockMagazine`, no `Magazine`. É também o transformer do Grabbable dele, para trocar entre pegada livre e trilho.
  - **Hierarquia:** o `Magazine` agora é filho de `MagwellAxis`, um eixo inclinado 19° (a inclinação medida no carregador) na posição do carregador inserido. A pose no mundo não mudou.
  - **Estados:**
    - **Inserido:** preso, parte da arma, sem pegada própria.
    - **No poço:** corre no eixo do cabo.
    - **Fora:** objeto com física (80 g), pegável e arremessável.
  - **Soltar:**
    - No controle, A/X numa mão que segura o corpo da arma aperta o retém do carregador; o botão afunda 1,5 mm.
    - O carregador sai empurrado pela mola e depois desce pelo peso, ao longo do eixo. Vazio ele cai sozinho, como na Gen5.
    - Com a arma de ponta-cabeça ele não cai, e dá para puxar com a mão.
    - Sai do poço depois de 12 cm no eixo, quando o topo passa do fundo do cabo.
  - **Encaixar:**
    - Segurando o carregador perto da boca do poço (até 1,5 cm do eixo, 25° de alinhamento), ele entra no trilho.
    - Empurrado até 55% do curso (`Auto Seat Insertion`), solta da mão, sobe o resto sozinho e trava. Pedido do usuário: a mão que segura a arma atrapalhava empurrar até o fim.
    - Puxado para fora sem chegar lá, volta a ficar livre na mão.
  - **Interpolação:** o corpo do carregador só interpola solto (caindo ou arremessado).
    - Dentro da arma ou na mão fica sem interpolação.
    - Um corpo cinemático interpolado, filho da arma em movimento, ficava atrasado e torto: o carregador inserido "flutuava" fora do cabo no primeiro teste.
  - **Zona de pegada:** modo `Magazine`, prioridade 60, ligada só com o carregador no poço ou fora.
    - O controlador não solta essa mão quando a outra larga a arma; só ferrolho e alavanca dependem da arma.
  - **Retém do ferrolho:** o carregador inserido liga `Empty Magazine Inserted` e, solto, desliga. Agora dá para testar o "puxar e soltar" da D3.
  - **Física:** o carregador ignora a colisão com os colliders da arma, porque fica dentro do cabo.
  - **Hand tracking:**
    - Encostar a ponta do polegar ou do indicador no retém do carregador e deixar lá por 0,15 s (`Touch Hold Time`).
    - A caixa `Touch Box` é bem justa na face do botão (8 × 12 × 12 mm), para precisar ser proposital. O usuário acertava sem querer com a caixa anterior.
    - As caixas de toque aparecem no Scene com o componente selecionado (gizmo laranja), também no retém do ferrolho e na alavanca.
    - Só vale com a arma na mão.
    - Puxar e encaixar com a mão já funcionavam.
  - **Debug:** o texto sobre a arma mostra o estado do carregador.
- [x] **D5. Empunhadura de desmontagem.** Gatilho analógico controla o curso do ferrolho.
  - **Estado:** concluída e testada no Quest, com controle e hand tracking. Adiantada a pedido do usuário, logo depois da D1.
    - O polegar ficou mais sensível depois do teste: `Thumb Input Range` passou de 0,05–0,4 para 0,02–0,15.
    - A posição da mão e da arma na pegada fica para o usuário ajustar depois.
  - **Vinda de uma pegada livre:** a zona de desmontagem aceita a outra mão numa pegada livre (`MustNotHoldGunExceptFree`).
    - Por exemplo, segurar a arma pela ponta com a esquerda e começar a desmontagem com a direita.
    - Ao pegar, a troca de mão solta a pegada livre.
    - Com a outra mão no cabo, continua valendo o arrasto do ferrolho (manobrar), para não virar desmontagem sem querer.
  - **Pegada (como na foto de referência):**
    - Palma no lado do ferrolho, virada para quem segura.
    - Dedos para cima, abraçando o ferrolho; polegar no beavertail empurrando a arma para a frente.
    - A zona aceita pelos dedos apontando para cima (até 50°), não mais pela palma para baixo. Na empunhadura os dedos apontam para a frente (~90°), então as duas não se confundem.
    - Volume: lado e topo da metade de trás do ferrolho.
    - A pose padrão foi refeita nessa posição. As poses do cabo, ajustadas pelo usuário, foram mantidas.
  - **No `GlockSlide`:** com uma mão no modo Takedown, recua até 8 mm (`Takedown Travel`). Na arma real são poucos milímetros, só para soltar a alavanca, então não conta como manobra.
    - Por padrão quem anda é a arma, para a frente, e o ferrolho fica parado na mão (`Takedown Moves Gun`).
  - **Controle:** gatilho analógico ou analógico, o maior dos dois (`Takedown Control` deixa escolher um só).
    - O analógico vale para os dois lados do eixo Y. Na pegada real o polegar empurra o frame para a frente enquanto os dedos puxam o ferrolho, então empurrar ou puxar fazem sentido.
  - **Hand tracking:** o quanto o polegar se mexeu desde a pegada, para qualquer lado (`Thumb Input Range`).
    - Com a palma virada para quem segura, o polegar fica bem visível, como o usuário confirmou no teste.
  - **Retém do ferrolho (D3, combinado):**
    - B/Y na mão do cabo levanta o retém com o ferrolho atrás e abaixa com ele travado.
    - Puxar e soltar o ferrolho também destrava.
    - Com carregador vazio inserido, trava aberto sozinho.
    - A/X fica para o retém do carregador (D4).
- [ ] **D6. Alavanca de desmontagem.** Grip + gatilho analógico.
  - **Estado:** montado no prefab, falta testar no Quest.
  - **Script:** `GlockTakedownLever`, na raiz. A alavanca não gira: desce 2,5 mm no rasgo (`Travel`) e volta pela mola.
  - **Controle:** mão na alavanca (zona `Zone_Lever`, com a outra mão segurando a arma) e o gatilho analógico puxa para baixo.
  - **Hand tracking:**
    - Pegando a alavanca, dobrar o indicador puxa (`Hand Input Range` 0,15–0,55).
    - Ou encostar o polegar e o indicador nas duas pontas dela, uma de cada lado (`Touch Box`), com a arma na mão.
    - As caixas foram aumentadas (28 × 26 × 30 mm) porque era difícil acertar no hand tracking.
  - **Corrida com o ferrolho (corrigida):**
    - A regra "só desce com o ferrolho recuado 2 mm" fazia a alavanca subir no instante em que o ferrolho voltava, antes de ele passar da bateria e prender a alavanca embaixo.
    - O ferrolho então não destravava. Falhava quase sempre no hand tracking e às vezes no controle.
    - Agora o ferrolho recuado só é exigido para a alavanca descer mais; a que já desceu fica enquanto a mão segura.
  - **Trava:** só desce com o ferrolho recuado pelo menos 2 mm (`Required Slide Offset`), o que a pegada de desmontagem faz. Com o ferrolho fechado, o ressalto do cano prende a alavanca.
  - **Para a D7:** `IsDown` (≥ 90% do curso).
  - **Debug:** o texto sobre a arma mostra o curso da alavanca.
- [x] **D7. Ferrolho sai pela frente.** Precisa da alavanca abaixada e do percussor disparado. Cano e mola passam a filhos do ferrolho.
  - **Estado:** concluída e testada no Quest.
    - Depois do teste o trilho até soltar ficou maior, a pedido do usuário (`Remove Travel` 3 → 8 cm), para o jogador ter que puxar.
    - O pulo para a frente ficou menor (`Pop Forward` 1,2 → 0,8 cm).
  - **Transformer:** o `GlockSlide` agora é o transformer do Grabbable do ferrolho (o `OneGrabTranslateTransformer` saiu).
    - No trilho, só o deslocamento em Z conta.
    - Fora da arma, a pegada é livre.
  - **Sequência:**
    1. Disparo a seco (percussor solto).
    2. Pegada de desmontagem recua o ferrolho.
    3. A outra mão abaixa a alavanca.
    4. Soltar a pegada de desmontagem: o ferrolho "pula" 0,8 cm à frente da bateria (`Pop Forward`) e a alavanca fica presa embaixo (`Latched`), então a outra mão já pode largar a alavanca.
    5. A outra mão pega o ferrolho e puxa para a frente. A 8 cm da bateria (`Remove Travel`) ele sai dos trilhos.
  - **Empurrar pela própria pegada de desmontagem (pedido do usuário):**
    - Com a alavanca abaixada, a entrada para o lado oposto ao de recuar empurra o ferrolho para a frente, proporcionalmente, até sair dos trilhos.
    - No controle é o analógico para o outro lado; no hand tracking, o polegar para o outro lado. O gatilho só recua.
    - Se o recuo foi pelo gatilho (o analógico não foi usado), empurrar o analógico para a frente leva o ferrolho para a frente e puxar recua.
    - O lado de recuar é o último usado com a alavanca em cima. Antes disso, qualquer lado recua, como antes.
    - O empurrão para em 60% do caminho (`Takedown Push Fraction`): o ferrolho fica meio para fora, o jogador larga a pegada de desmontagem e puxa o resto com a mão.
  - **Ferrolho à frente da bateria:**
    - Pegar nele é puxar pelo trilho, mesmo sem a outra mão segurando a arma (por exemplo, com a arma na mesa).
    - As zonas que pegariam a arma inteira pelo ferrolho (pegar pelo ferrolho, desmontagem) ficam bloqueadas enquanto isso.
  - **Zona da alavanca bloqueada enquanto ela está presa pelo ferrolho.** Antes, a mão que largava a alavanca para pegar o ferrolho pegava a alavanca de novo (zona de prioridade maior, com volume chegando embaixo do ferrolho), e o ferrolho não saía.
  - **Solto:**
    - O ferrolho vira peça solta: Rigidbody próprio (0,3 kg), cinemático na mão e com física fora dela, ignorando colisão com o frame.
    - A zona dele passa ao modo `Part` (qualquer mão pega, não depende da arma).
    - As zonas do frame que dependiam do ferrolho (desmontagem, pegar pelo ferrolho) ficam bloqueadas para novas pegadas. A mão que segura o frame continua.
  - **Cano e mola:** à frente da bateria andam junto com o ferrolho e, quando ele sai, viram filhos dele (o `GlockSlideLinkage` se desliga). A D8/D9 vai tirá-los.
  - **Sem os requisitos:** sem a alavanca abaixada ou com o percussor armado, o ferrolho não passa da bateria, como na arma real.
  - **Debug:** o texto mostra "(destravado)" e "fora da arma" na linha do ferrolho.
- [x] **D8. Tirar a mola do ferrolho.**
- [x] **D9. Tirar o cano do ferrolho.**
  - **Estado (D8 e D9):** concluídas e testadas no Quest, com controle e hand tracking.
  - **Script:** `GlockSlidePart`, no `RecoilSpringAssembly` e no `BarrelDriver`. É o transformer do Grabbable de cada peça.
  - **Requisitos:**
    - Só dá para pegar com o ferrolho fora da arma e seguro na outra mão (a zona fica `Blocked` até lá).
    - O cano só depois da mola.
  - **Saída:** a mão puxa a peça ao longo do caminho de saída, no espaço do ferrolho.
    - Mola, como na Glock real (pedido do usuário, com foto de referência):
      - É empurrada 3 mm contra a frente do ferrolho e inclina 15° em torno da ponta da frente (a traseira sai por baixo). Era 25°, ficava inclinada demais.
      - Depois é puxada para trás e para fora, 2,5 cm (era 4 cm).
      - O empurrão vai junto com a inclinação para a mão que puxa para trás não emperrar no começo.
      - Pivô medido da ponta da frente da malha.
    - Cano: por baixo e para trás, 3 cm.
    - A cada quadro a peça vai para o ponto do caminho em que o lugar pego por ela fica mais perto da mão, então basta puxar naturalmente.
    - Passou do fim, vira peça solta com física; solta antes, volta para o lugar pelo caminho.
    - Cano, do mesmo jeito: inclina 10° em torno da boca (a culatra sai primeiro por baixo) e é puxado para trás e um pouco para fora, 3,5 cm. Antes saía em diagonal.
  - **Zonas:** modo `Part`, prioridade 45 (mola) e 40 (cano), acima da zona do ferrolho (30).
    - Cada peça tem um `GrabVolume` (trigger) 3 cm maior em cada lado que ela.
    - A mola não saía: ela fica dentro do ferrolho, embaixo do cano, e a palma (que fica uns 3–4 cm atrás das pontas dos dedos) nunca chegava perto do collider dela.
  - **Física:** colliders próprios cobrindo a peça (Rigidbody de 50 g), cinemáticos e sem interpolação enquanto montados. Ignoram colisão com o resto da arma.
    - As caixas são medidas das malhas pelo builder, então acompanham um reexport do FBX.
    - O comprimento da mola interna também é medido da malha (bounds), no `GlockSlideLinkage`.
  - **Guia da mola:** o usuário alongou a guia direto na Unity; ficou bom e o reexport não é necessário por enquanto.
  - **Montagem de volta:** fase F.
  - **Bug corrigido no primeiro teste:** cano e mola ficavam presos no frame quando o ferrolho saía.
    - O builder recria as caixas de colisão a cada execução, e o `GlockSlide` guardava referências serializadas a elas.
    - Ao sair, essas referências vazias davam erro no meio do `Remove()`, antes de cano e mola virarem filhos do ferrolho.
    - Agora o ferrolho procura os colliders da arma na hora. Regra: não serializar referências a colliders criados pelo builder.

### E — Etapas guiadas

- [x] **E1. Máquina de estados das 8 etapas.** Valida a ordem e libera a próxima etapa.
- [x] **E2. Detectores das etapas.**
  - 3 ciclos completos do ferrolho.
  - Inspeção visual: olhar para a câmara aberta por X segundos.
  - Inspeção física: indicador ou controle encostando na câmara.
  - Apontar para zona segura + disparo seco.
  - **Estado (E1 e E2, feitas juntas):** testadas no Quest pelo usuário, funcionando.
    - A inspeção física às vezes passava sozinha: a mão que segura a arma ou o ferrolho ficava perto da câmara. Agora só conta a mão livre (`IsFingertipIn` com `freeHandsOnly`). Falta reconferir.
  - **Script:** `GlockTrainingSteps` (`Assets/_Project/Scripts/Training/`), no objeto `Training` da cena. Fica na cena porque a área segura e a câmera do jogador são dela.
  - **Ordem:** cada etapa só conta na vez dela. Ações fora de ordem não avançam e geram aviso (`WhenMistake`):
    - manobrar com o carregador dentro;
    - disparar antes de remover o carregador e inspecionar a câmara;
    - pôr o carregador de volta durante a desmontagem;
    - disparar fora da área segura.
  - **Detectores:**
    1. Carregador: o retém soltou o carregador.
    2. Manobras: 3 `WhenRacked` com o carregador fora.
    3. Inspeção visual: ferrolho aberto (≥ 85% do curso, travado ou seguro) e a cabeça olhando a câmara (até 45 cm, 20°) por 1,5 s.
    4. Inspeção física: ferrolho aberto e a ponta do indicador ou do polegar na caixa da câmara. Vale mão ou controle (`IsFingertipIn` ganhou a opção de incluir controles).
    5. Disparo a seco: `WhenStrikerReleased` com o cano apontado para a `SafeZone` (raycast da boca do cano).
    6. Desmontagem: o ferrolho saiu (`WhenRemoved`).
    7. e 8. Mola e cano saíram do ferrolho.
  - **Cena:**
    - `SafeZone`: caixa laranja no chão, à direita da mesa, com collider trigger.
    - `Training`: texto da etapa acima da mesa (`GlockTrainingLabel`, provisório até a E3), com o progresso e o aviso de erro em vermelho por 3 s.
  - **Gizmos:** com o `GlockTrainingSteps` selecionado, aparecem a caixa da câmara e a direção do cano.
- [ ] **E3. Feedback.** Destaque da peça, texto da etapa e aviso de erro.

### F — Montagem

- [ ] **F1. Etapas inversas.** Encaixe de cano, mola, ferrolho e carregador.

### G — Hand tracking

- [ ] **G1. Pegada por contato dos dedos**, com histerese.
- [ ] **G2. Gesto de empunhadura e snap no cabo.**
- [ ] **G3. Curso do ferrolho pelo fechamento dos dedos.** Alavanca pela pinça polegar + indicador.

### H — Ambiente e modos VR/MR

- [ ] **H2. Sons.** Montados com `Tools/Glock/Montar sons` (`GlockSoundsSetup`); falta ouvir no Quest.
  - **`GlockSounds`** (raiz da Glock):
    - Percussor disparando (dry fire).
    - Ferrolho chegando ao fim do curso (puxar) e voltando à bateria (soltar, mais alto quanto mais rápido volta).
    - Carregador soltando, ferrolho saindo da arma.
    - Mola e cano saindo do ferrolho: o mesmo som, a mola mais aguda. Tocam na fonte do ferrolho.
  - **`ImpactSound`** (arma, carregador, ferrolho, mola e cano):
    - Toca o som de queda com o volume pela velocidade do impacto. Tem cooldown, variação de pitch e ignora o primeiro meio segundo (as peças assentando).
    - Em colisor com **`ImpactSurface`** (a mesa e a caixa de desmuniciamento) toca o som "na mesa"; no resto, o do chão. Só a arma tem som próprio de chão; as outras peças usam o da mesa mais grave.
  - **Import:** mono, descomprimido ao carregar, Vorbis 70%.
- [ ] **H3. Caixa de desmuniciamento.** Modelada pelo desenho oficial (`D:\Blender\MilitaryBase\ClearingBox`) e montada com `Tools/Ambiente/Montar caixa de desmuniciamento na cena aberta` (`ClearingBoxSetup`); falta ver no Quest.
  - **Na cena:** fica à direita do jogador, virada para ele, com colisor de malha e `ImpactSurface`.
  - **SafeZone:** agora é o vão da caixa, em cima da areia (sem render). O disparo a seco vale apontando para dentro da caixa.

- [ ] **H1. Troca VR/MR.** Montada com `Tools/Modos/Montar troca VR-MR na cena aberta` (`ExperienceModeSetup`), falta testar no Quest.
  - **`ExperienceModeController`** (objeto `ExperienceMode` na B2_TestRig):
    - Carrega a `BackgroundIndoor` aditiva uma vez. Ela precisa estar no Build Settings; o setup coloca.
    - **VR:** liga os objetos que vieram ligados no ambiente e põe a cena do ambiente como ativa, para céu, luz ambiente e fog virem dela. Passthrough desligado.
    - **MR:** desliga o ambiente inteiro (render, colisores, luzes e volume de pós-processo), liga o `OVRPassthroughLayer` (underlay) do rig e limpa as câmeras com cor transparente.
    - Listas `VR Only` e `MR Only` para o que só vale num modo. No MR entram o `MR_Floor` (colisor invisível de 20 × 20 m em y = 0) e a `Directional Light` da cena de teste.
    - Lembra o último modo (`PlayerPrefs`).
  - **A mesa** (colisor, sem render) fica igual nos dois modos: no VR o ambiente desenha a mesma mesa no mesmo lugar; no MR ela é alinhada com a mesa de verdade (`TableCalibration`).
  - **`TableCalibration`** (alinhamento da mesa no MR):
    - Na primeira entrada no MR, ou pelo botão "AJUSTAR MESA" do pulso, aparece uma mesa fantasma (azul, transparente) que segue a mão direita ou o controle direito.
    - O jogador apoia a palma em cima da mesa real, no meio da borda mais perto dele, com os dedos apontando para o outro lado. Segurar a pinça esquerda, o gatilho esquerdo ou o A por 0,8 s confirma, e a fantasma some.
    - Quem se move é o rig (só yaw e posição), não a mesa: a arma, as peças e a área segura continuam na mesa virtual.
    - Voltar para o VR devolve o rig para a pose original, então tudo volta para a mesa do VR. Voltar para o MR reaplica o último alinhamento da sessão.
    - O `MR_Floor` vai para a altura do chão de verdade (o rig é Floor Level).
    - O ponto de referência (`TableReference`, filho da mesa) é o meio da borda de cima do lado do rig, calculado pelo setup a partir do BoxCollider.
    - Altura da palma e do controle acima do tampo e tempo de confirmação ficam no inspector.
  - **`WristModeToggle`**: botão de poke do ISDK na parte de baixo do pulso esquerdo (lado da palma).
    - Segue o pulso da mão no hand tracking ou, sem mão, o controle esquerdo.
    - Só aparece, e só aceita toque, com o lado da palma virado para a cabeça, com histerese.
    - Visual: seletor "MODO — VR | MR" de 7 × 4 cm, com a metade do modo atual acesa em laranja e a moldura mudando de cor ao passar e ao apertar o dedo. Posições, ângulos e cores ficam no inspector.
  - **Sem locomoção:** o `Locomotor` do rig já vinha desligado. Também foram desligados os grupos de teleporte e giro (analógico e microgesto da mão, nos dois lados) e tirado o `TeleportInteractable` do `Ground` do ambiente. Isso evita o analógico esquerdo disputar com a desmontagem e, no MR, não deslocar o mundo em relação à sala real.
  - **Passthrough:** `Insight Passthrough` = Supported no projeto e `isInsightPassthroughEnabled` ligado no OVRManager. O que mostra ou esconde o passthrough é a camada.
