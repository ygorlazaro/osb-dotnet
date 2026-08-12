DEFINT A - Z

PRINT "NUM v1.0 þ Toggle NUM LOCK status"
PRINT "Copyright (c) 1995 by PowerBASIC, Inc.  All Rights Reserved."
PRINT ""

Cmd$ = UCASE$( COMMAND$ )
IF ( Cmd$ = "ON" ) OR ( Cmd$ = "+" ) THEN
  Stat = -1
ELSEIF ( Cmd$ = "OFF" ) OR ( Cmd$ = "-" ) THEN
  Stat = 0
ELSE
  PRINT "Use:  NUM on|off|+|-"
  END 1
END IF

DEF SEG = 0
  X = PEEK( &H417 )
  IF Stat THEN
    X = X OR &B00100000
    PRINT "NUM LOCK est  ligado
  ELSE
    X = X AND &B11011111
    PRINT "NUM LOCK est  desligado
  END IF
  POKE &H417, X      'set keyboard toggles
DEF SEG
