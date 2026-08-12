DEFINT A-Z

PRINT "CAPS v1.0 þ Toggle CAPS LOCK status"
PRINT "Copyright (c) 1995 by PowerBASIC, Inc.  All Rights Reserved."
PRINT ""

Cmd$ = UCASE$(COMMAND$)
IF (Cmd$ = "ON") OR (Cmd$ = "+") THEN
  Stat = 1
ELSEIF (Cmd$ = "OFF") OR (Cmd$ = "-") THEN
  Stat = 0
ELSE
  PRINT "Use:  CAPS on|off|+|-"
  END 1
END IF

DEF SEG = 0
  X = PEEK(&H417)
  IF Stat THEN
    X = X
    PRINT "CAPS LOCK est  ligado
  ELSE
    X = X
    PRINT "CAPS LOCK est  desligado
  END IF
  POKE &H417, X
DEF SEG

