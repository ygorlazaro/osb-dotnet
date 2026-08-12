'====================================================================
' Quick Basic Forum
'   Date : 02-Jun-91
'   From : Larry Stone
'     To : Steve Halko
'Subject : Code to get disk drives
'===================================================================

'**** DRIVLIST.BAS - Drive Listing functions by Larry Stone, 1991
'**** Based on a routine published in MicroHelp's BUG Newsletter, 1/1/90

DECLARE FUNCTION FloppyDriveList$ ()
DECLARE FUNCTION HardDriveList$ (Floppies$)
'$INCLUDE: 'qb.bi'

CLS

HardDrives$ = HardDriveList$(Floppies$)
HardDrives% = LEN(HardDrives$)
Floppies% = LEN(Floppies$)
NumDrives% = HardDrives% + Floppies%

PRINT "Este sistema tem no total"; NumDrives%; "drives."
PRINT STRING$(80, 205);

PRINT "Este sistema tem"; Floppies%; "floppy drive(s) ==> ";
FOR N = 1 TO Floppies%
    PRINT MID$(Floppies$, N, 1); ": ";
NEXT
PRINT : PRINT STRING$(80, 205);

PRINT "Este sistema tem"; HardDrives%; "hard drive(s) ==> ";
FOR N = 1 TO HardDrives%
    PRINT MID$(HardDrives$, N, 1); ": ";
NEXT
PRINT
END

FUNCTION FloppyDriveList$
    DEF SEG = 0
    Floppies% = PEEK(&H410) \ 64 + 1   'How many floppy drives installed?
    DEF SEG                            'Back to DGROUP

    FOR N% = 1 TO Floppies%            'Place these letters into Floppies$
	scratch$ = scratch$ + CHR$(64 + N%)
    NEXT

    FloppyDriveList$ = scratch$        'Set the function.
    scratch$ = ""                      'Grabage collection.
END FUNCTION

FUNCTION HardDriveList$ (FloppyList$)
    DIM Reg AS RegType

    FloppyList$ = FloppyDriveList$     'Get the floppy drive list.
    Floppies% = LEN(FloppyList$)       'How many drives have we gotten?

				       'If only 1 floppy, first drive is C:
    HardDrive1% = Floppies% + 1 + ABS(Floppies% = 1)

    'DOS function AH = 44h & AL = 09h is "Device Driver Control (IOCTL)" and
    'tests whether a drive is local or remote.  If the carry flag is set then
    'AX will return 0Fh when drive letter is invalid.  This routine simply
    'checks to see if the carry flag is set and ends if it is.

    FOR BL% = HardDrive1% TO 26         'Roll through possible hard drives.
	Reg.AX = &H4409                 'DOS function "Device Driver Control"
	Reg.BX = BL%                    'Drive letter in BL register.
	INTERRUPT &H21, Reg, Reg        'Call Mr DOS (INT 21h).
	IF (Reg.Flags AND 1) THEN EXIT FOR        'Check carry flag.
	scratch$ = scratch$ + CHR$(64 + Reg.BX)   'Add the letter
    NEXT

    HardDriveList$ = scratch$           'Set the function.
    scratch$ = ""                       'Garbage collection.
END FUNCTION

