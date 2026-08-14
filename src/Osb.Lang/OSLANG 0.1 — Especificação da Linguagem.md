# OSLANG 0.1

**OSLANG — OSB Programming Language**

Especificação da linguagem de programação do OSB.

**Versão:** 0.1\
**Extensão:** `.osl`\
**Paradigma:** imperativo/procedural\
**Execução:** interpretada\
**Tipagem:** dinâmica, com tipo de variável estável após definido\
**Case sensitivity:** não\
**Ponto de entrada:** `MAIN()`

---

# 1. Introdução

OSLANG é uma linguagem de programação criada para o OSB, inspirada principalmente em BASIC, QBASIC e BC7.

A linguagem foi projetada para ser:

- simples de aprender;
- simples de implementar;
- legível;
- adequada para pequenos programas e scripts;
- suficientemente expressiva para aplicações básicas;
- integrada ao ambiente do OSB;
- extensível através de APIs fornecidas pelo sistema.

OSLANG não possui como objetivo competir com linguagens de propósito geral modernas. Sua prioridade é oferecer uma experiência simples e direta de programação dentro do OSB.

A linguagem deliberadamente evita construções como `GOTO`, classes e herança, favorecendo funções e estruturas de controle estruturadas.

---

# 2. Características gerais

OSLANG possui as seguintes características:

- linguagem case-insensitive;
- comandos orientados a linhas;
- ausência de `;` como terminador de instrução;
- tipagem dinâmica;
- tipo de variável estável após definido;
- variáveis locais e globais;
- funções;
- arrays homogêneos;
- `NULL`;
- condicionais;
- loops;
- tratamento de exceções;
- funções matemáticas básicas;
- entrada e saída padrão;
- extensões fornecidas pelo OSB.

---

# 3. Case-insensitivity

OSLANG não diferencia letras maiúsculas de minúsculas.

Os seguintes identificadores representam a mesma variável:

```osl
Nome
nome
NOME
NoMe
```

Da mesma forma, as seguintes formas representam a mesma palavra-chave:

```osl
PRINT
Print
print
PrInT
```

A documentação utiliza palavras-chave em letras maiúsculas por convenção.

---

# 4. Arquivos

Programas OSLANG utilizam a extensão:

```text
.osl
```

Exemplo:

```text
HELLO.OSL
CALCULATOR.OSL
GAME.OSL
```

Arquivos `.bas` pertencem ao BASIC legado do projeto e não fazem parte da linguagem OSLANG.

---

# 5. Estrutura de um programa

Todo programa OSLANG deve possuir uma função `MAIN`.

A execução começa pela chamada de `MAIN()`.

Exemplo:

```osl
FUNCTION MAIN()

    PRINT "Hello, OSB!"

END FUNCTION
```

Funções podem ser declaradas antes ou depois de `MAIN`.

---

# 6. Comentários

Comentários podem ser iniciados por `REM` ou por `'`.

Exemplo:

```osl
REM Este é um comentário

PRINT "Olá"
```

ou:

```osl
' Este também é um comentário

PRINT "Olá"
```

Comentários ocupam uma linha inteira na versão 0.1.

---

# 7. Linhas e terminadores

OSLANG não utiliza `;` para finalizar comandos.

Cada instrução normalmente ocupa uma linha.

Exemplo:

```osl
A = 10
B = 20
C = A + B

PRINT C
```

Estruturas de controle são delimitadas por `END`.

---

# 8. Identificadores

Identificadores podem conter:

- letras;
- números;
- `_`.

O primeiro caractere deve ser uma letra ou `_`.

Exemplos válidos:

```osl
Nome
idade
numero1
_nome
Numero_Favorito
```

Exemplos inválidos:

```text
1numero
numero-favorito
nome completo
```

Identificadores são case-insensitive.

---

# 9. Palavras reservadas

As palavras reservadas da OSLANG 0.1 são:

```text
AND
BOOLEAN
BOOL
BREAK
CATCH
CEIL
CLEAR
CONTINUE
COUNT
DO
ELIF
ELSE
END
FALSE
FLOOR
FOR
FUNCTION
GLOBAL
IF
INPUT
NOT
NULL
NUMBER
OR
POW
PRINT
RETURN
SQRT
STEP
STRING
STR
THEN
TO
TRUE
TRY
TYPEOF
VAR
WHILE
```

Embora algumas funções da biblioteca padrão sejam representadas por palavras reservadas nesta especificação, sua implementação deverá tratá-las como funções nativas sempre que possível.

`MAIN` é um nome especial de função, mas não é uma palavra reservada.

---

# 10. Tipos

OSLANG 0.1 possui os seguintes tipos:

```text
NUMBER
STRING
BOOLEAN
ARRAY
NULL
```

Arrays possuem internamente um tipo de elemento:

```text
ARRAY<NUMBER>
ARRAY<STRING>
ARRAY<BOOLEAN>
```

---

# 11. NUMBER

`NUMBER` representa números inteiros e decimais.

Exemplos:

```osl
A = 10
B = 10.5
C = -3
D = 0.25
```

A linguagem não diferencia `INTEGER`, `FLOAT`, `DOUBLE` ou outros tipos numéricos.

A implementação pode utilizar qualquer representação numérica apropriada internamente.

---

# 12. STRINGUs

`STRING` representa texto.

Strings são delimitadas por aspas duplas:

```osl
Nome = "Ygor"
Mensagem = "Olá, mundo!"
```

---

# 13. BOOLEAN

`BOOLEAN` possui dois valores:

```text
TRUE
FALSE
```

Exemplo:

```osl
Ativo = TRUE
Administrador = FALSE
```

---

# 14. NULL

`NULL` representa ausência de valor.

Exemplo:

```osl
Nome = NULL
```

`NULL` é diferente de:

```text
0
""
FALSE
```

`NULL` pode ser atribuído a qualquer variável, independentemente de seu tipo.

Exemplo:

```osl
VAR Nome STRING
VAR Idade NUMBER
VAR Ativo BOOLEAN

Nome = NULL
Idade = NULL
Ativo = NULL
```

A atribuição de `NULL` não altera o tipo da variável.

---

# 15. Tipagem dinâmica

OSLANG é dinamicamente tipada.

Quando uma variável é criada sem tipo explícito, seu tipo é determinado pela primeira atribuição de um valor diferente de `NULL`.

Exemplo:

```osl
Valor = 10
```

`Valor` passa a ser `NUMBER`.

Posteriormente:

```osl
Valor = 20
```

é válido.

Porém:

```osl
Valor = "20"
```

gera erro de runtime.

O tipo da variável não pode ser alterado depois de definido.

---

# 16. Variáveis

Variáveis podem ser criadas implicitamente através de atribuição:

```osl
Idade = 40
Nome = "Ygor"
Ativo = TRUE
```

Também podem ser declaradas explicitamente usando `VAR`.

```osl
VAR Idade NUMBER
VAR Nome STRING
VAR Ativo BOOLEAN
```

---

# 17. Valores padrão

Quando uma variável é declarada com tipo explícito, ela recebe o valor padrão daquele tipo.

```osl
VAR Nome STRING
```

equivale inicialmente a:

```text
Nome = ""
```

```osl
VAR Idade NUMBER
```

equivale inicialmente a:

```text
Idade = 0
```

```osl
VAR Ativo BOOLEAN
```

equivale inicialmente a:

```text
Ativo = FALSE
```

Uma variável declarada sem tipo:

```osl
VAR Valor
```

possui inicialmente:

```text
Valor = NULL
```

Seu tipo será determinado posteriormente pela primeira atribuição não nula.

---

# 18. Variáveis globais

Variáveis são locais por padrão.

Uma variável pode ser declarada como global utilizando `GLOBAL`.

```osl
GLOBAL Versao = "0.1"
```

Variáveis globais podem ser acessadas por qualquer função.

Exemplo:

```osl
GLOBAL NomeOS = "OSB"

FUNCTION MAIN()

    PRINT NomeOS

END FUNCTION
```

---

# 19. Escopo

Cada função possui seu próprio escopo local.

A resolução de uma variável ocorre na seguinte ordem:

1. escopo local;
2. parâmetros da função;
3. escopo global.

Uma variável local possui precedência sobre uma variável global de mesmo nome.

---

# 20. Truthiness

OSLANG utiliza uma regra de truthiness para condições.

São considerados `FALSE`:

```text
NULL
FALSE
0
""
```

São considerados `TRUE`:

```text
TRUE
qualquer NUMBER diferente de 0
qualquer STRING não vazia
qualquer ARRAY
```

Exemplo:

```osl
IF Nome THEN

    PRINT "Nome preenchido"

END
```

---

# 21. Comparação com NULL

`NULL` somente é igual a `NULL`.

Para verificar se uma variável possui valor nulo:

```osl
IF Nome = NULL THEN

    PRINT "Nome não informado"

END
```

Para verificar se não é nulo:

```osl
IF Nome <> NULL THEN

    PRINT "Nome informado"

END
```

Não existe coerção entre `NULL` e:

```text
0
""
FALSE
```

Portanto:

```text
NULL = 0
NULL = ""
NULL = FALSE
```

resulta em `FALSE`.

---

# 22. Arrays

Arrays são coleções homogêneas.

Exemplo:

```osl
Numeros = [1, 2, 3, 4, 5]
```

O tipo de `Numeros` será:

```text
ARRAY<NUMBER>
```

Outro exemplo:

```osl
Nomes = ["Ygor", "Lara", "Dante"]
```

resulta em:

```text
ARRAY<STRING>
```

---

# 23. Arrays homogêneos

Um array não pode conter elementos de tipos diferentes.

O seguinte é inválido:

```osl
Valores = [10, "Ygor", TRUE]
```

Um array também não pode receber posteriormente um elemento de tipo diferente.

```osl
Numeros = [1, 2, 3]

Numeros[0] = 10
```

é válido.

Porém:

```osl
Numeros[0] = "dez"
```

gera erro de runtime.

Estruturas heterogêneas poderão ser adicionadas futuramente, mas não fazem parte da OSLANG 0.1.

---

# 24. Índices de arrays

Arrays utilizam índice iniciado em zero.

```osl
Numeros = [10, 20, 30]

PRINT Numeros[0]
PRINT Numeros[1]
PRINT Numeros[2]
```

Resultado:

```text
10
20
30
```

O acesso fora dos limites gera erro de runtime.

---

# 25. Operadores aritméticos

A OSLANG 0.1 possui:

```text
+
-
*
/
%
```

Exemplo:

```osl
A = 10 + 5
B = 10 - 5
C = 10 * 5
D = 10 / 5
E = 10 % 3
```

`%` representa o resto da divisão.

---

# 26. Operador de concatenação

O operador `+` também concatena strings.

```osl
Nome = "Ygor"

Mensagem = "Olá, " + Nome
```

Resultado:

```text
Olá, Ygor
```

Valores não string podem ser convertidos para string durante concatenação.

```osl
Idade = 40

PRINT "Idade: " + Idade
```

---

# 27. Operadores de comparação

A OSLANG possui:

```text
=
<>
<
>
<=
>=
```

Exemplo:

```osl
IF Idade >= 18 THEN

    PRINT "Maior de idade"

END
```

`=` possui dois significados dependendo do contexto:

```osl
A = 10
```

representa atribuição.

Enquanto:

```osl
IF A = 10 THEN
```

representa comparação.

---

# 28. Operadores lógicos

A OSLANG possui:

```text
AND
OR
NOT
```

Exemplo:

```osl
IF Idade >= 18 AND Ativo THEN

    PRINT "Permitido"

END
```

---

# 29. Short-circuit

`AND` e `OR` utilizam avaliação de curto-circuito.

Em:

```osl
IF A <> 0 AND B / A > 10 THEN

    PRINT "Resultado"

END
```

se `A <> 0` for falso, `B / A > 10` não será avaliado.

Da mesma forma, em:

```osl
IF Nome = NULL OR Nome = "Ygor" THEN

    PRINT "Condição satisfeita"

END
```

a segunda expressão não será avaliada caso a primeira já seja verdadeira.

---

# 30. Precedência dos operadores

A precedência é:

```text
1. ()
2. []
3. NOT
4. * / %
5. + -
6. < > <= >= = <>
7. AND
8. OR
```

Exemplo:

```osl
Resultado = 2 + 3 * 4
```

resulta em:

```text
14
```

Enquanto:

```osl
Resultado = (2 + 3) * 4
```

resulta em:

```text
20
```

---

# 31. Funções

Funções são declaradas com `FUNCTION`.

```osl
FUNCTION Soma(A, B)

    RETURN A + B

END FUNCTION
```

Funções podem receber zero ou mais parâmetros.

```osl
FUNCTION Ola()

    PRINT "Olá!"

END FUNCTION
```

---

# 32. Parâmetros

Parâmetros podem ser utilizados dentro do escopo da função.

```osl
FUNCTION Saudacao(Nome)

    PRINT "Olá, " + Nome

END FUNCTION
```

Tipos podem ser especificados opcionalmente:

```osl
FUNCTION Soma(A NUMBER, B NUMBER)

    RETURN A + B

END FUNCTION
```

Os tipos declarados nos parâmetros seguem as mesmas regras das variáveis.

---

# 33. Retorno

Uma função pode retornar um valor utilizando `RETURN`.

```osl
FUNCTION Soma(A, B)

    RETURN A + B

END FUNCTION
```

A função pode então ser utilizada em uma expressão:

```osl
Resultado = Soma(10, 20)
```

Funções sem `RETURN` explícito retornam `NULL`.

---

# 34. Recursão

Funções podem chamar a si mesmas.

```osl
FUNCTION Fatorial(N)

    IF N <= 1 THEN
        RETURN 1
    END

    RETURN N * Fatorial(N - 1)

END FUNCTION
```

---

# 35. IF

A estrutura básica é:

```osl
IF condição THEN

    comandos

END
```

Exemplo:

```osl
IF Idade >= 18 THEN

    PRINT "Maior de idade"

END
```

---

# 36. ELIF

`ELIF` representa uma condição alternativa.

```osl
IF Nota >= 9 THEN

    PRINT "Excelente"

ELIF Nota >= 7 THEN

    PRINT "Bom"

ELIF Nota >= 5 THEN

    PRINT "Regular"

ELSE

    PRINT "Insuficiente"

END
```

`ELSEIF` não faz parte da linguagem.

---

# 37. ELSE

`ELSE` representa o caminho executado quando nenhuma condição anterior foi satisfeita.

```osl
IF Ativo THEN

    PRINT "Ativo"

ELSE

    PRINT "Inativo"

END
```

---

# 38. FOR

A estrutura `FOR` utiliza `TO`.

```osl
FOR I = 1 TO 10

    PRINT I

END
```

O valor final é inclusivo.

O exemplo executa dez iterações.

---

# 39. STEP

`STEP` define o incremento do `FOR`.

```osl
FOR I = 1 TO 10 STEP 2

    PRINT I

END
```

Resultado:

```text
1
3
5
7
9
```

Também é possível utilizar valores negativos:

```osl
FOR I = 10 TO 1 STEP -1

    PRINT I

END
```

---

# 40. WHILE

`WHILE` avalia a condição antes de cada iteração.

```osl
I = 1

WHILE I <= 10

    PRINT I
    I = I + 1

END
```

---

# 41. DO WHILE

`DO WHILE` também avalia a condição antes da execução.

```osl
DO WHILE I < 10

    PRINT I

END
```

Portanto, diferentemente de um `do...while` tradicional de algumas linguagens, o bloco pode executar zero vezes.

---

# 42. BREAK

`BREAK` interrompe o loop atual.

```osl
FOR I = 1 TO 100

    IF I = 10 THEN
        BREAK
    END

    PRINT I

END
```

`BREAK` somente pode ser utilizado dentro de loops.

---

# 43. CONTINUE

`CONTINUE` interrompe a iteração atual e inicia a próxima.

```osl
FOR I = 1 TO 10

    IF I % 2 = 0 THEN
        CONTINUE
    END

    PRINT I

END
```

O exemplo imprime apenas os números ímpares.

---

# 44. TRY/CATCH

A OSLANG possui tratamento básico de exceções.

```osl
TRY

    comandos

CATCH ERR

    comandos

END
```

Exemplo:

```osl
TRY

    Valor = NUMBER("abc")

CATCH ERR

    PRINT ERR

END
```

`ERR` representa o erro ocorrido durante a execução do bloco.

`ERR` é válido dentro do bloco `CATCH`.

---

# 45. STDIO

A biblioteca padrão de entrada e saída possui:

```text
PRINT
INPUT
CLEAR
```

---

# 46. PRINT

`PRINT` imprime uma expressão.

```osl
PRINT "Olá"
PRINT 10 + 20
PRINT Nome
```

Também pode receber múltiplas expressões:

```osl
PRINT "Nome:", Nome
```

---

# 47. INPUT

`INPUT` lê uma entrada do usuário.

O valor retornado por `INPUT` é sempre `STRING`.

Exemplo:

```osl
PRINT "Digite sua idade:"

INPUT Idade

Idade = NUMBER(Idade)
```

A conversão é explícita.

---

# 48. CLEAR

`CLEAR` limpa a saída padrão do ambiente.

```osl
CLEAR
```

---

# 49. Funções de conversão

A linguagem fornece:

```text
STR()
NUMBER()
BOOL()
```

## STR

Converte um valor para `STRING`.

```osl
Texto = STR(123)
```

## NUMBER

Converte um valor para `NUMBER`.

```osl
Numero = NUMBER("123")
```

Uma conversão inválida gera erro de runtime.

## BOOL

Converte um valor para `BOOLEAN` utilizando as regras de truthiness da linguagem.

```osl
A = BOOL(0)
B = BOOL("Olá")
C = BOOL(NULL)
```

resulta em:

```text
A = FALSE
B = TRUE
C = FALSE
```

---

# 50. Funções matemáticas

A biblioteca matemática básica faz parte do core da linguagem.

## SQRT

Calcula a raiz quadrada.

```osl
Resultado = SQRT(25)
```

## ABS

Retorna o valor absoluto.

```osl
Resultado = ABS(-10)
```

## POW

Calcula uma potência.

```osl
Resultado = POW(2, 10)
```

## FLOOR

Arredonda para baixo.

```osl
Resultado = FLOOR(10.9)
```

## CEIL

Arredonda para cima.

```osl
Resultado = CEIL(10.1)
```

---

# 51. COUNT

`COUNT()` retorna a quantidade de elementos de uma `STRING` ou `ARRAY`.

```osl
Nome = "Ygor"

PRINT COUNT(Nome)
```

Resultado:

```text
4
```

Para arrays:

```osl
Numeros = [10, 20, 30]

PRINT COUNT(Numeros)
```

Resultado:

```text
3
```

Para `NULL`:

```osl
COUNT(NULL)
```

retorna:

```text
0
```

Para tipos incompatíveis, `COUNT()` gera erro de runtime.

---

# 52. TYPEOF

`TYPEOF()` retorna o tipo de um valor como `STRING`.

```osl
A = 10

PRINT TYPEOF(A)
```

Resultado:

```text
NUMBER
```

Exemplos:

```osl
TYPEOF(10)
TYPEOF("Olá")
TYPEOF(TRUE)
TYPEOF(NULL)
```

retornam:

```text
NUMBER
STRING
BOOLEAN
NULL
```

Para qualquer array:

```text
TYPEOF([1, 2, 3])
```

retorna:

```text
ARRAY
```

---

# 53. Extensões do OSB

O core da OSLANG é independente do OSB.

Funcionalidades específicas do sistema são disponibilizadas através de módulos de extensão.

A arquitetura conceitual é:

```text
OSLANG
   │
   ├── Runtime
   ├── Standard Library
   │
   └── Extension API
          │
          ├── OSB.Shell
          ├── OSB.Xwin
          └── futuras extensões
```

Uma extensão pode registrar funções e comandos adicionais no runtime.

---

# 54. OSB.Shell

O `Osb.Shell` poderá fornecer uma extensão específica para OSLANG.

Essa extensão poderá disponibilizar operações como:

```text
PWD()
DIR()
CD()
MKDIR()
DEL()
TYPE()
```

Essas funções não fazem parte do core da linguagem.

Elas pertencem ao ambiente OSB.

Isso mantém OSLANG independente do Shell.

---

# 55. Princípio da API de extensões

O runtime não deve expor diretamente objetos ou classes .NET para programas OSLANG.

As extensões devem registrar explicitamente as operações que estarão disponíveis para a linguagem.

Isso permite:

- controle de segurança;
- independência entre runtime e host;
- testes isolados;
- diferentes ambientes de execução;
- evolução independente do OSB.

---

# 56. Erros

A implementação deve distinguir pelo menos quatro categorias:

### Erro léxico

Um caractere ou token inválido.

### Erro sintático

Uma estrutura inválida da linguagem.

### Erro semântico

Uma construção válida sintaticamente, mas inválida segundo as regras da linguagem.

Exemplo:

```osl
BREAK
```

fora de um loop.

### Erro de runtime

Um erro ocorrido durante a execução.

Exemplos:

```osl
A = 10 / 0
```

ou:

```osl
Numero = NUMBER("abc")
```

---

# 57. Mensagens de erro

Mensagens de erro devem informar, sempre que possível:

- categoria do erro;
- número da linha;
- coluna;
- descrição do problema.

Exemplo:

```text
OSLANG ERROR
Line 12, Column 15

Division by zero.
```

---

# 58. Funcionalidades fora da versão 0.1

Não fazem parte da OSLANG 0.1:

- `GOTO`;
- `GOSUB`;
- classes;
- objetos;
- interfaces;
- herança;
- generics;
- módulos definidos pelo usuário;
- namespaces;
- `IMPORT`;
- lambdas;
- closures;
- async/await;
- threads;
- ponteiros;
- structs;
- enums;
- dicionários/maps;
- arrays heterogêneos;
- `FOR EACH`;
- tipos definidos pelo usuário;
- acesso arbitrário ao .NET;
- reflexão;
- execução arbitrária de código C#.

Essas funcionalidades podem ser consideradas em versões futuras.

---

# 59. Programa completo de exemplo

O seguinte programa utiliza grande parte dos recursos da OSLANG 0.1:

```osl
GLOBAL PROGRAM = "OSB"
GLOBAL VERSION = "0.1"


FUNCTION Soma(A NUMBER, B NUMBER)

    RETURN A + B

END FUNCTION


FUNCTION Fatorial(N NUMBER)

    IF N <= 1 THEN
        RETURN 1
    END

    RETURN N * Fatorial(N - 1)

END FUNCTION


FUNCTION MAIN()

    CLEAR

    PRINT PROGRAM + " OSLANG " + VERSION
    PRINT "======================"
    PRINT ""

    PRINT "Digite seu nome:"
    INPUT Nome

    IF Nome = NULL OR Nome = "" THEN

        PRINT "Nome nao informado."

    ELSE

        PRINT "Ola, " + Nome + "!"

    END

    PRINT ""

    PRINT "Digite um numero:"
    INPUT Numero

    Numero = NUMBER(Numero)

    PRINT "Tipo: " + TYPEOF(Numero)
    PRINT "Quadrado: " + POW(Numero, 2)
    PRINT "Raiz: " + SQRT(ABS(Numero))

    PRINT ""

    FOR I = 1 TO 5

        PRINT "Contagem: " + I

    END

    PRINT ""

    Resultado = Soma(10, 20)

    PRINT "10 + 20 = " + Resultado

    PRINT ""

    TRY

        Valor = NUMBER("abc")

    CATCH ERR

        PRINT "Erro capturado: " + ERR

    END

END FUNCTION
```

---

# 60. Princípios para evolução

A OSLANG deve preservar três princípios:

### Simplicidade

Novos recursos somente devem ser adicionados quando trouxerem benefício significativo.

### Consistência

Uma mesma ideia deve possuir comportamento uniforme em toda a linguagem.

### Extensibilidade

Funcionalidades específicas do OSB devem preferencialmente ser implementadas através da API de extensões, e não adicionadas como novas palavras-chave.

O core da linguagem deve permanecer pequeno.

---

# 61. Resumo normativo da OSLANG 0.1

```text
LINGUAGEM
    OSLANG
    case-insensitive
    .osl

TIPOS
    NUMBER
    STRING
    BOOLEAN
    ARRAY<T>
    NULL

DECLARAÇÃO
    VAR
    GLOBAL

CONTROLE
    IF
    ELIF
    ELSE
    FOR
    TO
    STEP
    WHILE
    DO WHILE
    BREAK
    CONTINUE

FUNÇÕES
    FUNCTION
    RETURN
    MAIN

EXCEÇÕES
    TRY
    CATCH ERR

OPERADORES
    +
    -
    *
    /
    %
    =
    <>
    <
    >
    <=
    >=
    AND
    OR
    NOT

STDIO
    PRINT
    INPUT
    CLEAR

CONVERSÃO
    STR()
    NUMBER()
    BOOL()

MATEMÁTICA
    SQRT()
    ABS()
    POW()
    FLOOR()
    CEIL()

UTILIDADES
    COUNT()
    TYPEOF()

VALORES FALSY
    NULL
    FALSE
    0
    ""

SHORT-CIRCUIT
    AND
    OR

NÃO EXISTE
    GOTO
    GOSUB
    classes
    objetos
    imports
    arrays heterogêneos
    acesso arbitrário ao .NET
```

---

# 62. Status

Esta especificação define o contrato funcional da **OSLANG 0.1**.

A implementação do lexer, parser, AST, runtime e biblioteca padrão deverá seguir esta especificação.

Qualquer funcionalidade não definida neste documento não deve ser considerada parte da OSLANG 0.1.
