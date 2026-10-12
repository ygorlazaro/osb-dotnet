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
- Entrada de **teclado PS/2** (scancodes set 1) com um mini-shell:
  `VER`, `ABOUT`, `HELP`, `CLS`, `ECHO <texto>`, `TICKS`, `SLEEP <s>`, `HALT`.

## Arquitetura

```
src/Osb.Kernel/
├── Osb.Kernel.csproj   # projeto C# "sem .NET": NoStdLib, nada referenciado
├── MiniCoreLib.cs      # mini-CoreLib: System.Object/String/primitivas + stubs que o
│                       # ilc exige do "system module" (ThrowHelpers, Buffer, TypeCast...)
├── Kernel.cs           # kernel: VGA + serial + IDT/PIC + teclado IRQ1 + shell OSB
├── boot.S              # bootstrap Multiboot2: 32-bit -> long mode -> chama kmain
│                       # (+ helpers: outb/inb, mem*, lidt, sti/cli, halt)
├── interrupts.S        # gerado no build: 256 stubs de ISR + isr_common + tabela de stubs
├── linker.ld           # layout ELF (carregado em 0x100000)
├── grub.cfg            # menu do GRUB (multiboot2)
└── build.sh            # build completo -> osb.iso
```

Pipeline de build:

```
C# (mini-CoreLib)  ──csc──▶  OsbKernel.dll (IL)
OsbKernel.dll      ──ilc──▶  OsbKernel.o   (objeto x86-64 freestanding)
boot.S + interrupts.S ─clang▶ boot.o + interrupts.o
boot.o + interrupts.o + OsbKernel.o ──ld.lld + linker.ld─▶ kernel.elf (Multiboot2 ELF)
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
./build.sh run        # build + boot no QEMU (serial no terminal)
./build.sh check      # valida que kernel.elf é Multiboot2
```

No QEMU com `-display none`, use `./build.sh run` (serial no stdio) ou:

```sh
qemu-system-x86_64 -cdrom osb.iso -m 64M -display none -serial stdio -no-reboot
```

O kernel também escreve na tela VGA; para ver a janela, basta remover `-display none`.

## Próximos passos sugeridos

- **RTC (IRQ8)** com hora de verdade e mantendo a contagem durante o boot (hoje o
  uptime começa no `kmain`).
- **Agendador**: múltiplas "tarefas" cooperativas no kernel usando o tick (round-robin).
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
- **SSE requer CR0.EM=0**: com `CR0.EM=1` toda instrução SSE do código C# (ex.
  `movups` de um stackalloc) sofre `#UD`. O `boot.S` força CR0 (PG|NE|WP, EM/TS=0)
  e CR4 (PAE|OSFXSR|OSXMMEXCPT) na entrada do long mode.