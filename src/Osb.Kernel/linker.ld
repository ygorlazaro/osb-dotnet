OUTPUT_FORMAT(elf64-x86-64)
ENTRY(_start)

SECTIONS
{
    . = 0x100000;

    __kernel_start = .;

    .multiboot2 ALIGN(8) : { KEEP(*(.multiboot2)) }

    .text ALIGN(4096) : {
        __text_start = .;
        *(.text .text.*)
        __text_end = .;
    }

    .rodata ALIGN(4096) : {
        __rodata_start = .;
        *(.rodata .rodata.*)
        *(.dotnet_eh_table)
        *(.userstub)
        __rodata_end = .;
    }

    .modules ALIGN(8) : {
        __Modules_start = .;
        *(__modules)
        __Modules_end = .;
    }

    .data ALIGN(4096) : {
        __data_start = .;
        *(.data .data.*)
        __data_end = .;
    }

    .bss ALIGN(4096) : {
        __bss_start = .;
        *(COMMON)
        *(.bss .bss.*)
        __bss_end = .;
    }

    __kernel_end = .;

    /DISCARD/ : {
        *(.note .note.*)
        *(.comment)
        *(.eh_frame .eh_frame.*)
        *(.eh_frame_hdr)
    }
}
