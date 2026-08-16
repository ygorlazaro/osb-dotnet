# OSLANG 0.62 Specification

**Language:** OSLANG  
**Version:** 0.62  
**Base Version:** 0.61  
**File Extension:** `.osl`  
**Release Type:** Additive / Non-Breaking  
**Breaking Changes:** None

## 1. Overview

OSLANG 0.62 introduces data interchange and network capabilities through the `OSL` namespace.

New modules:

- `OSL.JSON`
- `OSL.CSV`
- `OSL.XML`
- `OSL.CNF`
- `OSB.NET`

A principal característica desta versão é permitir que programas OSLANG trabalhem com:

- JSON
- CSV
- XML
- arquivos de configuração do OSB
- APIs HTTP
- dados externos
- conversão entre dados externos e objetos OSLANG

A versão não adiciona novas estruturas fundamentais à linguagem, exceto o necessário para a conversão de dados externos em tipos OSLANG.

## 2. OSL Namespace

OSLANG já possui o conceito de módulos através de `USING`.

O 0.62 expande o namespace `OSL`.

Exemplo:

```osl
USING OSL.JSON
USING OSL.CSV
USING OSL.XML
USING OSL.CNF
```

Os módulos são independentes.

Um programa só precisa carregar os módulos utilizados.

## 3. OSL.JSON

`OSL.JSON` fornece operações para serialização, desserialização e manipulação de JSON.

```osl
USING OSL.JSON
```

### 3.1 PARSE

`PARSE()` converte uma string JSON em valores OSLANG.

```osl
Data = JSON.PARSE("{""name"":""Ygor"",""age"":40}")
```

O resultado pode conter:

- `OBJECT`
- `ARRAY`
- `STRING`
- `NUMBER`
- `BOOLEAN`
- `NULL`

JSON objects são representados como objetos/dicionários de propriedades.

Exemplo:

```osl
JSON = """
{
    "name": "Ygor",
    "age": 40,
    "active": true
}
"""

Data = JSON.PARSE(JSON)

PRINT Data.name
PRINT Data.age
PRINT Data.active
```

### 3.2 STRINGIFY

`STRINGIFY()` converte um valor OSLANG em JSON.

```osl
User = NEW User()

User.Name = "Ygor"
User.Age = 40

JSON = OSL.JSON.STRINGIFY(User)

PRINT JSON
```

Arrays também são suportados:

```osl
Numbers = [1, 2, 3, 4, 5]

JSON = OSL.JSON.STRINGIFY(Numbers)
```

### 3.3 PRETTY

JSON pode ser formatado:

```osl
JSON = OSL.JSON.STRINGIFY(Data, TRUE)
```

O segundo parâmetro indica que o JSON deve ser formatado de maneira legível.

## 4. JSON Values

JSON possui seis tipos fundamentais:

- `OBJECT`
- `ARRAY`
- `STRING`
- `NUMBER`
- `BOOLEAN`
- `NULL`

OSLANG faz o mapeamento:

| JSON | OSLANG |
| ---- | ------ |
| `object` | `OBJECT` |
| `array` | `ARRAY` |
| `string` | `STRING` |
| `number` | `NUMBER` |
| `true`/`false` | `BOOLEAN` |
| `null` | `NULL` |

JSON não possui um tipo equivalente a `ENUM`.

## 5. JSON Objects

Objetos JSON devem permitir acesso por propriedade:

```osl
Data = OSL.JSON.PARSE("""
{
    "name": "Ygor",
    "age": 40
}
""")

PRINT Data.name
PRINT Data.age
```

Também devem permitir acesso dinâmico:

```osl
Key = "name"

PRINT Data[Key]
```

Objetos JSON devem fornecer:

- `KEYS()`
- `VALUES()`
- `CONTAINS()`
- `COUNT()`

Exemplo:

```osl
IF Data.CONTAINS("name") THEN
    PRINT Data.name
END
```

## 6. OSL.CSV

`OSL.CSV` fornece leitura e escrita de arquivos CSV.

```osl
USING OSL.CSV
```

### 6.1 PARSE

```osl
Data = OSL.CSV.PARSE("""
name,age,active
Ygor,40,true
John,25,false
""")
```

O resultado é um `ARRAY`.

Cada linha pode ser representada como um `OBJECT`.

```osl
FOR Row IN Data

    PRINT Row.name
    PRINT Row.age

END
```

### 6.2 HEADER

CSV deve suportar arquivos com cabeçalho.

```osl
Data = OSL.CSV.PARSE(Content, TRUE)
```

Quando `TRUE` é informado, a primeira linha é utilizada como nome das propriedades.

### 6.3 STRINGIFY

```osl
CSV = OSL.CSV.STRINGIFY(Data)
```

### 6.4 FILE

O módulo deve oferecer operações diretamente sobre arquivos:

- `READ()`
- `WRITE()`
- `APPEND()`

Exemplo:

```osl
Data = OSL.CSV.READ("users.csv")

FOR User IN Data
    PRINT User.name
END
```

## 7. OSL.XML

`OSL.XML` fornece operações básicas para XML.

```osl
USING OSL.XML
```

### 7.1 PARSE

```osl
Document = OSL.XML.PARSE("""
<user>
    <name>Ygor</name>
    <age>40</age>
</user>
""")
```

### 7.2 STRINGIFY

```osl
XML = OSL.XML.STRINGIFY(Document)
```

### 7.3 FILE

```osl
Document = OSL.XML.READ("config.xml")

OSL.XML.WRITE(Document, "output.xml")
```

### 7.4 Navegação

O objeto XML deve permitir operações básicas:

- `NAME()`
- `VALUE()`
- `ATTRIBUTES()`
- `CHILDREN()`
- `CHILD()`
- `HAS()`

Exemplo:

```osl
User = OSL.XML.PARSE(Content)

Name = User.CHILD("name")

PRINT Name.VALUE()
```

A implementação não precisa transformar XML em um objeto OSLANG convencional.

O runtime pode possuir uma representação própria de XML.

## 8. OSL.CNF

`OSL.CNF` é específico do ambiente OSB.

Ele representa o formato de arquivos de configuração utilizado pelo sistema.

```osl
USING OSL.CNF
```

O objetivo é evitar que aplicações OSLANG precisem conhecer a estrutura física dos arquivos `*.CFG` / `*.CNF`.

### 8.1 READ

```osl
Config = OSL.CNF.READ("OSB.CFG")
```

### 8.2 GET

```osl
Value = Config.GET("COLOR")
```

### 8.3 SET

```osl
Config.SET("COLOR", "RED")
```

### 8.4 HAS

```osl
IF Config.HAS("COLOR") THEN
    PRINT Config.GET("COLOR")
END
```

### 8.5 SAVE

```osl
Config.SAVE()
```

Também deve existir:

```osl
OSL.CNF.WRITE(Config, "OSB.CFG")
```

### 8.6 DELETE

```osl
Config.DELETE("TEMP_VALUE")
```

### 8.7 KEYS

```osl
FOR Key IN Config.KEYS()

    PRINT Key

END
```

## 9. OSL.CNF e o OSB

`OSL.CNF` é deliberadamente diferente de `OSL.JSON`.

JSON é um formato genérico.

CNF é uma API de configuração do OSB.

Isso permite que futuramente o formato físico do `OSB.CFG` mude sem quebrar aplicações OSLANG.

Exemplo:

```osl
USING OSL.CNF

Config = OSL.CNF.READ("OSB.CFG")

IF Config.HAS("LANGUAGE") THEN
    PRINT Config.GET("LANGUAGE")
END
```

## 10. OSB.NET

O `OSB.NET` será o módulo de comunicação de rede do ambiente OSB.

```osl
USING OSB.NET
```

A implementação inicial deve ser pequena.

O objetivo do 0.62 é estabelecer a fundação.

## 11. PING

`PING()` testa conectividade com um host.

```osl
Result = OSB.NET.PING("google.com")
```

O resultado deve ser um objeto contendo informações básicas.

Conceitualmente:

- `success`
- `host`
- `time`

Exemplo:

```osl
Result = OSB.NET.PING("example.com")

IF Result.success THEN
    PRINT "Online"
    PRINT Result.time
ELSE
    PRINT "Offline"
END
```

## 12. DOWN

`DOWN()` realiza uma requisição HTTP e retorna o conteúdo.

```osl
Content = OSB.NET.DOWN("https://example.com")
```

Inicialmente `DOWN()` pode realizar uma requisição HTTP GET.

O comportamento deve ser equivalente conceitualmente a:

```text
curl URL
```

Mas integrado ao runtime OSLANG.

## 13. DOWN com opções

A forma básica:

```osl
Content = OSB.NET.DOWN(URL)
```

deve continuar existindo.

Uma segunda forma pode receber opções:

```osl
Response = OSB.NET.DOWN(URL, Options)
```

Exemplo:

```osl
Options = {
    "timeout": 5000,
    "headers": {
        "Accept": "application/json"
    }
}

Response = OSB.NET.DOWN(URL, Options)
```

O objeto de resposta deve permitir futuramente:

- `STATUS`
- `HEADERS`
- `BODY`

## 14. Métodos HTTP

A primeira implementação pode começar somente com:

- `PING()`
- `DOWN()`

Mas o desenho da API deve permitir posteriormente:

- `GET()`
- `POST()`
- `PUT()`
- `PATCH()`
- `DELETE()`
- `HEAD()`

Minha sugestão é não implementar todos no 0.62 ainda.

A arquitetura deve nascer preparada para isso.

## 15. JSON + NET

Uma das combinações mais importantes da versão é:

```osl
Response = OSB.NET.DOWN("https://example.com/api/users")

Data = OSL.JSON.PARSE(Response)

FOR User IN Data

    PRINT User.name

END
```

Esse é provavelmente o primeiro caso de uso que deve virar um teste de integração da linguagem.

## 16. PARSE

Aqui eu faria uma pequena alteração em relação à ideia original.

Eu não colocaria `PARSE` como keyword imediatamente.

Primeiro, eu faria `PARSE` como operação de biblioteca:

```osl
Data = OSL.JSON.PARSE(Content)
Data = OSL.XML.PARSE(Content)
Data = OSL.CSV.PARSE(Content)
```

Isso resolve 90% dos casos sem criar uma nova construção sintática.

## 17. PARSE para classes do usuário

Entretanto, a ideia de transformar uma resposta externa em uma classe OSLANG é muito boa.

Eu deixaria isso preparado para uma futura extensão:

```osl
User = PARSE(UserClass, JSON)
```

Exemplo futuro:

```osl
CLASS User

    PUBLIC Name String
    PUBLIC Age Number
    PUBLIC Active Boolean

END
```

Então:

```osl
JSON = OSB.NET.DOWN("https://example.com/user/1")

User = PARSE(User, JSON)
```

Isso criaria:

```text
JSON
 ↓
PARSE
 ↓
User
```

E permitiria futuramente:

```osl
Users = PARSE(User[], JSON)
```

Mas eu deixaria a implementação dessa forma para uma versão posterior.

No 0.62, os parsers das bibliotecas devem produzir valores genéricos.

## 18. Conversão entre dados externos e classes

O runtime deve futuramente suportar:

```text
JSON  → OBJECT
OBJECT → JSON

JSON  → CLASS
CLASS → JSON

XML   → OBJECT
OBJECT → XML

CSV   → ARRAY
ARRAY → CSV
```

Isso deve ser construído sobre uma infraestrutura comum de mapeamento.

Eu chamaria internamente de `ObjectMapper`, e não implementaria uma lógica completamente diferente em JSON, XML e CSV.

## 19. Tratamento de erros

Todas as operações de IO, parsing e rede devem funcionar naturalmente com:

```osl
TRY

    Data = OSL.JSON.PARSE(Content)

CATCH ERR

    PRINT ERR

END
```

Erros devem continuar utilizando o mecanismo existente de `ERR`.

Não devemos criar uma segunda forma de exception handling para essas APIs.

## 20. Arquivos

Os módulos devem aceitar tanto conteúdo quanto arquivos quando fizer sentido.

Por exemplo:

```osl
JSON = OSL.JSON.PARSE(Content)
```

e:

```osl
JSON = OSL.JSON.READ("data.json")
```

Da mesma forma:

```osl
XML = OSL.XML.PARSE(Content)
XML = OSL.XML.READ("data.xml")
```

e:

```osl
CSV = OSL.CSV.PARSE(Content)
CSV = OSL.CSV.READ("data.csv")
```

Isso mantém uma API consistente.

## 21. Arquitetura recomendada

```text
OSLANG
│
├── OSL
│   │
│   ├── JSON
│   │   ├── PARSE
│   │   ├── STRINGIFY
│   │   └── READ / WRITE
│   │
│   ├── CSV
│   │   ├── PARSE
│   │   ├── STRINGIFY
│   │   └── READ / WRITE
│   │
│   ├── XML
│   │   ├── PARSE
│   │   ├── STRINGIFY
│   │   └── READ / WRITE
│   │
│   └── CNF
│       ├── READ
│       ├── WRITE
│       ├── GET
│       ├── SET
│       ├── HAS
│       └── DELETE
│
└── OSB
    │
    └── NET
        ├── PING
        └── DOWN
```

Isso mantém uma distinção que considero importante:

- `OSL` = biblioteca geral da linguagem
- `OSB` = capacidades específicas do sistema operacional

## 22. Exemplo completo

Um programa real em 0.62 poderia ser:

```osl
USING OSL.JSON
USING OSB.NET

FUNCTION MAIN()

    TRY

        Response = OSB.NET.DOWN(
            "https://example.com/api/users"
        )

        Users = OSL.JSON.PARSE(Response)

        FOR User IN Users

            PRINT "${User.name} - ${User.email}"

        END

    CATCH ERR

        PRINT "Unable to retrieve users."
        PRINT ERR

    END

END
```

E com uma classe futuramente:

```osl
CLASS User

    PUBLIC Name String
    PUBLIC Email String
    PUBLIC Age Number

END
```

a evolução natural seria:

```osl
Response = OSB.NET.DOWN(URL)

Users = PARSE(User, Response)
```

## 23. O que eu deliberadamente NÃO colocaria no 0.62

Eu seguraria:

- `POST`
- autenticação avançada
- cookies
- multipart
- WebSockets
- XML XPath completo
- XSLT
- JSON Schema
- serialização avançada
- reflection pública
- generics
- async/await

Não porque não sejam interessantes, mas porque o 0.62 deveria estabelecer o modelo de dados externo.

A sequência ideal seria:

```text
0.61
Linguagem expressiva
        ↓
0.62
Dados + IO + HTTP básico
        ↓
0.63
Object Mapping / PARSE
        ↓
0.64
HTTP completo
        ↓
0.7
Standard Library madura
```

## 24. Principal Recomendação

Eu faria uma regra para o 0.62:

> JSON, CSV e XML são formatos. CNF é configuração do OSB. NET é infraestrutura do OSB.

Isso parece uma distinção pequena, mas evita que daqui a algumas versões tenhamos uma gigantesca `OSL` que sabe fazer tudo.

E eu manteria `PARSE` fora da gramática por enquanto. Primeiro faria:

```osl
OSL.JSON.PARSE()
OSL.XML.PARSE()
OSL.CSV.PARSE()
```

Depois, quando tivermos uma implementação sólida de reflection/mapeamento de classes, introduzimos:

```osl
PARSE(User, Data)
```

como uma operação da própria linguagem.

Isso também combina muito bem com a arquitetura que o repositório já está construindo: o 0.61 já tem uma camada explícita de Extensibility, além de AST, compilation e diagnostics, então o 0.62 pode crescer como standard library/runtime, sem precisar transformar cada nova capacidade em keyword.
