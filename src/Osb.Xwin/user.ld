/* Linker script for an OSB ring-3 program.
 *
 * The image is linked at VA 0x400000, exactly where the kernel maps a
 * process's private block (Memory/Layout.cs: UserBase). The kernel copies the
 * resulting flat binary to that address and jumps to the entry offset.
 *
 * The user stack lives at the top of the block (Layout.UserStackTop); the
 * image may grow up to just below it. */

OUTPUT_FORMAT(elf64-x86-64)
ENTRY(_start)

SECTIONS
{
    . = 0x2000000;

    __image_start = .;

    .text ALIGN(16) : {
        *(.text .text.*)
    }

    .rodata ALIGN(16) : {
        *(.rodata .rodata.*)
        *(.dotnet_eh_table)
        *(.userstub)
    }

    .data ALIGN(16) : {
        *(.data .data.*)
    }

    .bss ALIGN(16) : {
        *(COMMON)
        *(.bss .bss.*)
    }

    __image_end = .;

    /DISCARD/ : {
        *(.note .note.*)
        *(.comment)
        *(.eh_frame .eh_frame.*)
        *(.eh_frame_hdr)
    }
}
