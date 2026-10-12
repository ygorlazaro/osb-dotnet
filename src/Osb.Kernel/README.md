# Osb.Kernel — o kernel bootável do OSB

Primeira versão **bootável** do OSB (reboot do projeto original de 2000 em QBasic),
escrita em **C# puro freestanding** no estilo do projeto [Cosmos](https://github.com/CosmosOS/Cosmos):
sem sistema operacional hospedeiro, sem framework externo — compilada com **NativeAOT**
(sem runtime, sem BCL) e um **bootloader próprio Multiboot2**.

## O que esta versão faz

- Boota via **GRUB (Multiboot2)** em x86-64 (long mode), a partir de um ISO (BIOS).
- Kernel C# compilado nativamente com `ilc` (NativeAOT) contra um **mini-CoreLib próprio**
  (`MiniCoreLib.cs`) — as únicas "bibliotecas" necessárias para o compilador C# funcionar
  sem um OS.
- Saída de texto em **VGA text mode** (`0xB8000`) espelhada na **COM1 (serial)**.
- **IDT (256 gates) + PIC 8259A** remapeado (IRQ0-15 → vetores 32-47): exceções de CPU
  viram panic legível (vetor + RIP), e o **teclado PS/2 é dirigido por interrupção**
  (IRQ1 → vetor 33, fila circular de scancodes, EOI explícito) — sem polling.
- **Timer PIT** (8254, canal 0) a **100 Hz via IRQ0** (vetor 32): contador global de ticks,
  uptime em segundos e os comandos `TICKS`/`SLEEP <segundos>` (espera dirigida por ticks).
- **Clock RTC** (CMOS, IRQ8 → vetor 40): modo 24h + binário, interrupção "update-ended"
  (~1 Hz) que mantém o relógio de software do kernel; comandos `TIME` e `DATE` exibem
  hora e data reais.
- **Agendador round-robin preemptivo**: a shell (tarefa 0) e uma tarefa "demo" (tarefa 1,
  pilha própria de 8 KiB) alternam o CPU por **preempção**: o handler do PIT (IRQ0) troca
  de contexto a cada tick (troca de pilha via `osb_switch`), sem `yield` das tarefas. O
  frame do ISR interrompido fica "estacionado" na pilha de cada tarefa até ela ser
  reescalonada. A demo imprime uma linha por segundo e dorme com `halt()` entre rodadas;
  o idle da shell também dorme com `halt()` — CPU quase ociosa, mas a demo continua
  rodando mesmo durante um `SLEEP` da shell.
- **Desligamento/reboot**: `POWEROFF` faz ACPI S5 (porta PM1a_CNT 0x604) — no QEMU o
  emulador encerra limpo (útil: nada fica preso capturando teclado/mouse do host);
  `REBOOT` pulsa o reset via controlador 8042.
- Entrada de **teclado PS/2** (scancodes set 1) com um mini-shell:
  `VER`, `ABOUT`, `HELP`, `CLS`, `ECHO <texto>`, `TICKS`, `SLEEP <s>`, `TIME`, `DATE`,
  `HALT`, `POWEROFF`, `REBOOT`.

## Arquitetura

```
src/Osb.Kernel/
├── Osb.Kernel.csproj   # projeto C# "sem .NET": NoStdLib, nada referenciado
├── MiniCoreLib.cs      # mini-CoreLib: System.Object/String/primitivas + stubs que o
│                       # ilc exige do "system module" (ThrowHelpers, Buffer, TypeCast...)
├── Kernel.cs           # kernel: VGA + serial + IDT/PIC + teclado IRQ1 + RTC IRQ8
│                       # + agendador RR cooperativo + shell OSB
├── boot.S              # bootstrap Multiboot2: 32-bit -> long mode -> chama kmain
│                       # (+ helpers: outb/inb, mem*, lidt, sti/cli, halt)
├── sched.S             # troca de contexto (osb_switch) + trampoline da tarefa demo
├── interrupts.S        # gerado no build: 256 stubs de ISR + isr_common + tabela de stubs
├── linker.ld           # layout ELF (carregado em 0x100000)
├── grub.cfg            # menu do GRUB (multiboot2)
└── build.sh            # build completo -> osb.iso
```

Pipeline de build:

```
C# (mini-CoreLib)  ──csc──▶  OsbKernel.dll (IL)
OsbKernel.dll      ──ilc──▶  OsbKernel.o   (objeto x86-64 freestanding)
boot.S + sched.S + interrupts.S ─clang▶ boot.o + sched.o + interrupts.o
boot.o + sched.o + interrupts.o + OsbKernel.o ──ld.lld + linker.ld─▶ kernel.elf (Multiboot2 ELF)
kernel.elf         ──grub-mkrescue─▶       osb.iso
```

## Pré-requisitos

- .NET SDK 10.x
- clang, lld (ld.lld), xorriso, grub-mkrescue (grub-pc-bin), nasm (opcional)
- qemu-system-x86_64 (para testar)

## Build e execução

```sh
cd src/Osb.Kernel
chmod +x build.sh
./build.sh            # gera src/Osb.Kernel/osb.iso
./build.sh run        # build + boot no QEMU (serial no terminal, sem janela)
./build.sh run-gui    # idem, mas abre a janela VGA (cuidado com o grab de input)
./build.sh check      # valida que kernel.elf é Multiboot2
```

`run` usa `-display none -serial stdio`: sem janela, então o QEMU **não captura** o
teclado/mouse do host. O kernel espelha a VGA na serial, então você vê tudo no terminal.
Para sair: digite `POWEROFF` no prompt do OSB (ACPI S5 → o QEMU encerra sozinho).

Se rodar com janela (`run-gui` ou QEMU manual), o grab de input pode prender o mouse/
teclado da sessão: **Ctrl+Alt+G** libera o grab e **Ctrl+Alt+2** abre o monitor do QEMU
(depois `quit`). O comando `POWEROFF` também encerra o emulador e libera o input.

## Próximos passos sugeridos

- **Espaços de endereço / threads**: hoje as duas tarefas compartilham o espaço do kernel;
  trocar CR3 por tarefa e separar pilhas de kernel (TSS.rsp0) habilitaria ring 3.
- ACPI + MMIO, framebuffer via `multiboot2` (modo gráfico) em vez de VGA text.
- Migrar comandos do `Osb.Shell` ("VER", "DIR" em memória, OSLANG) para o kernel.
- ABI de saída: hoje o "sistema" é o kernel; depois, um user-space.

## Notas

- O ilc é localizado automaticamente no cache NuGet
  (`runtime.linux-x64.microsoft.dotnet.ilcompiler`).
- `MiniCoreLib.cs` é a fronteira do "runtime": tudo que o C# exige sem um OS.
  Manter pequena; nada de GC/alocação — o kernel usa buffers fixos/stackalloc.
- `interrupts.S` é gerado deterministicamente pelo `build.sh` (256 stubs: cada um
  empurra `[vetor][dummy]`, descartando o error code da CPU nos vetores 8/10-14/17).
  O `isr_common` salva **GPRs + XMM0-15** (o handler SysV pode sujar registradores
  vetorizados do código interrompido), realinha a stack em 16 bytes, chama
  `osb_handle_interrupt` e faz `iretq`.
- O shell "dorme" com `sti; hlt` quando a fila de scancodes está vazia; a IRQ1
  entrega o scancode e a IRQ0 (100 Hz) acorda o idle a cada tick.
- **Qualquer IRQ do slave (IRQ8-15) exige o cascateamento no master**: sem IRQ2
  desmascarado no 8259A master (OCW1 = `0xF8` = IRQ0/IRQ1/IRQ2), a IRQ8 do RTC nunca
  chega à CPU. `0xF8`, e não `0xFB` (que mascara justamente IRQ0 e IRQ1).
- **Preempção**: `SchedulerTick` roda dentro do handler da IRQ0 (portanto com IF=0, logo
  atômica) e faz `osb_switch` para a outra tarefa. O EOI é enviado **antes** da troca,
  para o PIC poder entregar o próximo tick já na tarefa nova. Quando a tarefa é
  reescalonada, ela retoma de dentro do próprio `SchedulerTick`, desenrola o handler,
  restaura GPRs+XMM do frame e faz `iretq` — o contexto interrompido volta intacto.
- `osb_switch` só salva os registradores **callee-saved** (rbx, rbp, r12-r15). Isso basta
  porque o `isr_common` já salva **todos** os GPRs e XMM no frame de interrupção antes de
  chamar o handler: o que a troca de contexto precisa preservar são só os callee-saved
  vivos entre o `iretq` e o ponto de troca.
- A tarefa demo tem pilha fixa de 8 KiB em `.bss`; a IRQ pode interrompê-la e roda na
  própria pilha da tarefa (sem TSS), então o espaço precisa cobrir ISR + handler.
- **SSE requer CR0.EM=0**: com `CR0.EM=1` toda instrução SSE do código C# (ex.
  `movups` de um stackalloc) sofre `#UD`. O `boot.S` força CR0 (PG|NE|WP, EM/TS=0)
  e CR4 (PAE|OSFXSR|OSXMMEXCPT) na entrada do long mode.