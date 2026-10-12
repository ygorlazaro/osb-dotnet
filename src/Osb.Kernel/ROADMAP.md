# ROADMAP — do kernel protótipo ao OSB "ring 3"

Este documento planeja a evolução do OSB de *kernel único rodando tudo em ring 0*
para um sistema **multitarefa e multiusuário**, com **interface estilo MS-DOS** e
**ext4** como sistema de arquivos on-disk.

> Estado atual (após `feat(kernel): preemptive round-robin`): ring 0 apenas,
> escalonador preemptivo de kernel, console VGA/serial, teclado/timer/RTC por IRQ.
> Tudo ainda é o próprio kernel.

---

## 1. Princípios de projeto

1. **Não é Unix-like.** Nada de "tudo é arquivo", `/etc`, `/usr`, pipes, `fork()`.
   A usabilidade imita o **MS-DOS**:
   - *drive letters* (`C:`, `D:`), separador `\`, diretório corrente **por drive**;
   - comandos **internos** (no interpretador): `DIR`, `CD`, `COPY`, `DEL`, `REN`,
     `TYPE`, `CLS`, `VER`, `ATTRIB`, `SET`, `PROMPT`...;
   - **comandos externos** = programas `.EXE` carregados do disco.
2. **Multitarefa preemptiva** (já temos a base no kernel; precisamos expô-la a
   processos de usuário).
3. **Multiusuário**, mas no idioma DOS (ver §3) — não no modelo uid 0/setuid do Unix.
4. **ext4 é o formato on-disk.** Reaproveita o ecossistema Linux (`mke2fs`,
   `debugfs`, `fsck`) para criar e inspecionar a imagem; a *apresentação* é DOS-like.
5. **API de sistema estilo DOS**: syscalls via `INT 21h`, número da função em `AH`.
6. Sem SMAP/SMEP/IST por enquanto (endurecimento vem depois).

## 2. Hierarquia de camadas (dependentes de baixo para cima)

```
L8  Jobs/redirecionamento, arquivos .BAT, variáveis de ambiente, PATH
L7  init (PID 1), serviços/daemons em background, supervisão
L6  Privilégio em user mode: conta ADMIN, elevação autenticada (≈ "sudo" DOS-ish)
L5  Identidade: contas, grupos, LOGIN, autenticação (hash de senha), sessões/consoles
L4  FS: ext4 (leitura→escrita), drive letters, dono+modo vindos do disco
L3  Programas de usuário: loader + runtime mínimo (.EXE)
L2  Processos: espaço de endereço próprio (CR3 por processo), spawn/exit/wait
L1  ABI de syscalls: INT 21h, validação de ponteiros, falhas de usuário recuperáveis
L0  Privilégio de CPU: ring 3 (GDT user + TSS + paging U/S + gate DPL3)
```

Cada camada **não funciona** sem as de baixo. `sudo` depende de contas (L5) que
dependem de FS com dono (L4) que depende de processos isolados (L2).

## 3. Decisões de design (e o que ainda está aberto)

| Tema | Decisão |
|---|---|
| Syscall | `INT 21h`, função em `AH`, args em `RSI/RDI/RDX/...`, retorno em `RAX` |
| Vetor 21h livre | **Remapear o PIC para 0x30–0x3F** (IRQ0=0x30, IRQ1=0x31, IRQ8=0x38) — sem isso colide com a IRQ1 (teclado) |
| Paths | `drive:\dir\file`, `\` como separador, diretório corrente por drive |
| Multiusuário | contas (nome, hash de senha, home, flag *admin*); sessão por console |
| Privilégio | sem `uid 0`/`setuid`; comando `ADMIN` autentica e eleva a sessão (análogo ao "sudo"); *a confirmar* |
| Permissões de arquivo | substrato = `uid/gid/mode` do ext4 (já existe no disco); UI = DOS-like (`ATTRIB`, dono) — *a confirmar* |
| Formato de executável | ELF interno exposto como `.EXE` (mais fácil de toolchainar) — *a confirmar* |
| Dispositivo de bloco | ATA PIO (IDE, `-drive if=ide`) — simples; AHCI/virtio depois |
| ext4 simplificado | começar com `-O ^has_journal ^extent ^64bit ^metadata_csum`, depois habilitar extents |

## 4. Fatias verticais (cada uma funciona ponta a ponta)

### Fatia 1 — Ring 3 no ar + ABI `INT 21h`  ← **em implementação**
- GDT com segmentos de usuário (DPL3) + TSS + `ltr`; `TSS.RSP0` = pilha de kernel.
- Paging: marcar `PML4[0]`/`PDPT[0]`/`PD[2]` como U=1 (só a página 0x400000–0x5FFFFF; kernel continua U=0).
- IDT: vetor `0x21` como gate **DPL3**.
- `enter_user` (iretq para CPL3) + programa de usuário (blob copiado para 0x400000).
- Syscalls: `AH=09h` (imprime string `$`), `AH=4Ch` (termina).
- Falhas vindas de CPL3 são **recuperáveis** (matam a tarefa, não o sistema).
- Nova tarefa de escalonador (tarefa 2) que roda o programa em ring 3.
- Comandos: `RING3` e `RING3 BAD` (teste de proteção: `cli` em ring 3 → #GP).
- **Aceite:** o blob imprime via `INT 21h` e termina; `RING3 BAD` mostra o #GP
  tratado; o demo/shell continuam rodando (multitarefa preservada).

### Fatia 2 — Processos com espaço de endereço próprio
- Struct de processo (PID, pai, estado, CR3, pilhas, uid, dir/drive corrente).
- Page tables próprias por processo; troca de CR3 no escalonador; `TSS.RSP0` por processo.
- `spawn`/`exit`/`wait` (ou `fork`-like + `exec`-like).
- **Aceite:** dois processos isolados — um não consegue ler a memória do outro (#PF).

### Fatia 3 — Consoles/sessões + LOGIN (multiusuário)
- Múltiplos consoles (ex.: Alt+F1..F4) ou sessões serial.
- Tabela de contas em memória; `LOGIN.EXE`; cada sessão tem usuário + drive/dir corrente.
- **Aceite:** dois usuários logados ao mesmo tempo, cada um com sua sessão/estado.

### Fatia 4 — ext4 somente-leitura
- Driver ATA PIO; superbloco, group descriptors, inode/block bitmaps, extent tree,
  entradas de diretório, leitura de arquivo.
- Montar a imagem como `C:`.
- **Aceite:** `DIR C:\` e `TYPE C:\...` leem uma imagem ext4 real criada com `mke2fs`.

### Fatia 5 — ext4 leitura/escrita + comandos DOS
- Alocação de blocos/inodes; criar/apagar/escrever.
- Comandos: `DIR CD TYPE COPY DEL MKDIR REN ATTRIB`.
- **Aceite:** criar arquivo pela shell, reiniciar, arquivo persiste.

### Fatia 6 — Permissões multiusuário sobre ext4
- Checar `uid/gid/mode` do ext4 em toda operação de FS; conta `ADMIN`; elevação; home por usuário.
- **Aceite:** usuário A não lê o arquivo do B; admin lê; elevação funciona.

### Fatia 7 — init + serviços (daemons)
- `INIT.EXE` como PID 1; lista de serviços; processos de background; comando `TASKS`.
- **Aceite:** um serviço roda em background e sobrevive.

### Fatia 8 — Conforto DOS
- `.BAT`, `SET`/variáveis de ambiente, `PATH`, redirecionamento simples.
- **Aceite:** um `.BAT` encadeia comandos; `PATH` acha um `.EXE` fora do diretório corrente.

## 5. Plano do ext4 (Fatia 4–5)

- **Dispositivo:** ATA PIO (portas 0x1F0–0x1F7), IRQ14 opcional (polling primeiro).
- **Imagem:** gerada no build se não existir:
  `mke2fs -t ext4 -O ^has_journal,^extent,^64bit,^metadata_csum -b 4096 disk.img`
- **Ordem de implementação:** superbloco → group desc → bitmap de inodes → inode →
  ponteiros de bloco (diretos/indiretos) → diretório → leitura; em seguida escrita e extents.
- **Ferramenta de teste:** `debugfs` no host para criar/listar arquivos na imagem.

## 6. Riscos e questões abertas

- **`INT 21h` × IRQ1:** resolvido remapeando o PIC (Fatia 1).
- **Permissões:** substrato ext4 (`mode/uid/gid`) × atributos DOS — decidir na Fatia 4/6.
- **Formato `.EXE`:** ELF renomeado × formato próprio — decidir na Fatia 3.
- **Isolamento real** exige CR3 por processo (Fatia 2) — ring 3 sozinho só dá privilégio.
- **Sem GC no kernel:** evitar alocação; usar buffers fixos em `.bss` (como já é feito).
