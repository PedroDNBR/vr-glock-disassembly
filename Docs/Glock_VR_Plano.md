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
- **Mola recuperadora:** comprime com escala no eixo dela.
- **Hierarquia muda com o estado:**
  - Montada: cano e mola seguem o `BarrelDriver` do frame.
  - Ferrolho solto: cano e mola viram filhos do ferrolho.
  - Depois: cada peça pode sair como objeto livre.
- **Retém do ferrolho (`slide_release`):** precisa travar o ferrolho aberto, para a inspeção visual e física.

```
Glock (frame, segurável)
├─ Frame, gatilho, trava do gatilho, alavanca, retém do ferrolho, retém do carregador
├─ Carregador (encaixe próprio)
├─ SlideGroup (trilho com limites)
│   └─ Ferrolho, extrator, tampa traseira
└─ BarrelDriver (pose calculada pelo curso do ferrolho)
    └─ Cano, conjunto da mola (haste, capa, mola)
```

## Tarefas

Uma de cada vez. Cada tarefa só termina quando o item "Pronto quando" estiver conferido.

### A — Preparar o modelo

- [ ] **A1. Conferir os pivôs.** Comparar a origem de cada peça `_LP` com o ponto em que ela gira ou desliza (pino do gatilho, eixo da alavanca, retém do ferrolho, trilho do ferrolho, eixo do carregador).
  *Pronto quando:* há uma lista do que está certo e do que precisa mudar.
- [ ] **A2. Corrigir os pivôs e reexportar.** Ajustar as origens no Blender sem mover a geometria e reexportar o FBX.
  *Pronto quando:* na Unity, cada peça gira ou desliza no lugar certo ao mexer no Transform.
- [ ] **A3. Montar o prefab `Glock`.** Hierarquia acima, a partir do FBX.
  *Pronto quando:* o prefab abre com as peças nos lugares certos e os pais corretos.

### B — Setup XR

- [ ] **B1. Instalar os pacotes e configurar para Quest.** Meta XR Core SDK, Interaction SDK, build target Android, OpenXR e URP com configurações mobile (MSAA 4x, sem HDR e sem pós-processamento).
  *Pronto quando:* o Project Validation da Meta fica sem erros.
- [ ] **B2. Cena de teste com rig mínimo.** Controles e mãos, com grab e poke, sem locomoção.
  *Pronto quando:* dá para pegar um cubo com o controle no editor (Link) ou no Quest.

### C — Pegar a arma

- [ ] **C1. Zonas de pegada no frame.**
  - Cabo: snap.
  - Cabo + ferrolho: desmontagem.
  - Ferrolho.
  - Alavanca.
  - Resto: livre.
  *Pronto quando:* cada zona ativa o modo certo, com prioridade e ângulo de aceitação.
- [ ] **C2. Gatilho.** O gatilho da arma segue o gatilho do controle na empunhadura. Registrar o estado do percussor (armado ou disparado).

### D — Mecânica da arma (sem validação de etapas)

- [ ] **D1. Ferrolho no trilho.** Limite de curso e retorno por mola.
- [ ] **D2. Cano e mola seguindo o curso do ferrolho.** Curvas e pivô.
- [ ] **D3. Retém do ferrolho.** Travar e destravar o ferrolho aberto.
- [ ] **D4. Carregador removível.** Retém do carregador e encaixe de volta.
- [ ] **D5. Empunhadura de desmontagem.** Gatilho analógico controla o curso do ferrolho.
- [ ] **D6. Alavanca de desmontagem.** Grip + gatilho analógico.
- [ ] **D7. Ferrolho sai pela frente.** Precisa da alavanca abaixada e do percussor disparado. Cano e mola passam a filhos do ferrolho.
- [ ] **D8. Tirar a mola do ferrolho.**
- [ ] **D9. Tirar o cano do ferrolho.**

### E — Etapas guiadas

- [ ] **E1. Máquina de estados das 8 etapas.** Valida a ordem e libera a próxima etapa.
- [ ] **E2. Detectores das etapas.**
  - 3 ciclos completos do ferrolho.
  - Inspeção visual: olhar para a câmara aberta por X segundos.
  - Inspeção física: indicador ou controle encostando na câmara.
  - Apontar para zona segura + disparo seco.
- [ ] **E3. Feedback.** Destaque da peça, texto da etapa e aviso de erro.

### F — Montagem

- [ ] **F1. Etapas inversas.** Encaixe de cano, mola, ferrolho e carregador.

### G — Hand tracking

- [ ] **G1. Pegada por contato dos dedos**, com histerese.
- [ ] **G2. Gesto de empunhadura e snap no cabo.**
- [ ] **G3. Curso do ferrolho pelo fechamento dos dedos.** Alavanca pela pinça polegar + indicador.
