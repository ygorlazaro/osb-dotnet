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
- Entrada de **teclado PS/2** (polling, scancodes set 1) com um mini-shell:
  `VER`, `ABOUT`, `HELP`, `CLS`, `ECHO <texto>`, `HALT`.

## Arquitetura

```
src/Osb.Kernel/
├── Osb.Kernel.csproj   # projeto C# "sem .NET": NoStdLib, nada referenciado
├── MiniCoreLib.cs      # mini-CoreLib: System.Object/String/primitivas + stubs que o
│                       # ilc exige do "system module" (ThrowHelpers, Buffer, TypeCast...)
├── Kernel.cs           # kernel: VGA + serial + teclado PS/2 + shell OSB
├── boot.S              # bootstrap Multiboot2: 32-bit -> long mode -> chama kmain
├── linker.ld           # layout ELF (carregado em 0x100000)
├── grub.cfg            # menu do GRUB (multiboot2)
└── build.sh            # build completo -> osb.iso
```

Pipeline de build:

```
C# (mini-CoreLib)  ──csc──▶  OsbKernel.dll (IL)
OsbKernel.dll      ──ilc──▶  OsbKernel.o   (objeto x86-64 freestanding)
boot.S             ──clang─▶ boot.o
boot.o + OsbKernel.o ──ld.lld + linker.ld─▶ kernel.elf (Multiboot2 ELF)
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

- IDT + PIC (interrupções) para teclado real (hoje é polling) e timer (PIT/RTC).
- ACPI + MMIO, framebuffer via `multiboot2` (modo gráfico) em vez de VGA text.
- Migrar comandos do `Osb.Shell` ("VER", "DIR" em memória, OSLANG) para o kernel.
- ABI de saída: hoje o "sistema" é o kernel; depois, um user-space.

## Notas

- O ilc é localizado automaticamente no cache NuGet
  (`runtime.linux-x64.microsoft.dotnet.ilcompiler`).
- `MiniCoreLib.cs` é a fronteira do "runtime": tudo que o C# exige sem um OS.
  Manter pequena; nada de GC/alocação — o kernel usa buffers fixos/stackalloc.