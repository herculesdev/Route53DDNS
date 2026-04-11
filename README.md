# Route53DDNS

Um utilitário em .NET 8 para atualização dinâmica de registros do AWS Route 53 com o seu IP público atual, funcionando de forma similar a um serviço de DNS Dinâmico (DDNS).

---

## 🚀 Propósito e Contexto

O **Route53DDNS** foi desenvolvido para resolver o problema de hosts em redes domésticas ou pequenos escritórios que possuem IPs públicos dinâmicos, mas precisam ser acessados via domínios gerenciados no AWS Route 53.

A ferramenta monitora continuamente o IP externo da rede (via `api.ipify.org`) e, ao detectar uma alteração, sincroniza automaticamente os registros configurados na AWS. 

> [!NOTE]
> Atualmente o serviço realiza a detecção de **IPv4**. Embora suporte a configuração de registros do tipo `AAAA`, a atualização automática dependerá do IP detectado.

## 🛠️ Tecnologias Utilizadas

- **Linguagem:** C# 12
- **Framework:** .NET 8.0 (Worker Service)
- **SDK AWS:** `AWSSDK.Route53`
- **Resiliência:** `Polly` (para retentativas de chamadas HTTP)
- **Testes:** xUnit e `NSubstitute` (Mocking)
- **Containerização:** Docker (Alpine/Aspnet runtime)

## ⚙️ Configuração

O projeto utiliza o sistema de configuração padrão do .NET (`appsettings.json`, Variáveis de Ambiente, etc).

### 1. Arquivo `appsettings.json`

Crie um arquivo chamado `appsettings.json` no diretório `Route53DDns/`:

```json
{
  "AwsConfig": {
    "AccessKey": "sua_aws_access_key",
    "SecretKey": "sua_aws_secret_key",
    "TargetHostedZone": "exemplo.com",
    "TargetRecords": [
      {
        "Name": "home.exemplo.com",
        "Type": "A"
      },
      {
        "Name": "*.home.exemplo.com",
        "Type": "A"
      }
    ],
    "Interval": 60
  }
}
```

- `AccessKey`/`SecretKey`: Credenciais do IAM (opcional se utilizar IAM Roles ou variáveis de ambiente).
- `TargetHostedZone`: O nome da zona hospedada no Route 53.
- `TargetRecords`: Lista de registros que devem ser atualizados.
- `Interval`: Intervalo de checagem em segundos.

### 2. Variáveis de Ambiente

Qualquer configuração presente no `appsettings.json` pode ser substituída por variáveis de ambiente seguindo o padrão do .NET (`Seção__Chave`).

Exemplo para as credenciais da AWS:
- `AwsConfig__AccessKey`
- `AwsConfig__SecretKey`

Para propriedades de tipos complexos como **arrays** (`TargetRecords`), utilize o índice numérico:
- `AwsConfig__TargetRecords__0__Name` (ex: `home.exemplo.com`)
- `AwsConfig__TargetRecords__0__Type` (ex: `A`)
- `AwsConfig__TargetRecords__1__Name` (ex: `*.home.exemplo.com`)
- `AwsConfig__TargetRecords__1__Type` (ex: `A`)

Outros exemplos:
- `AwsConfig__TargetHostedZone`
- `AwsConfig__Interval`

> [!TIP]
> Caso `AccessKey` e `SecretKey` não sejam fornecidos, a aplicação delega a autenticação para o comportamento automático do SDK da AWS (**Default Credential Chain**). Isso é ideal para ambientes que utilizam IAM Roles (como instâncias EC2 ou ECS Tasks), onde o SDK resolve as credenciais automaticamente.

## 🚀 Como Buildar e Rodar

### Execução Local (.NET SDK)

1. Clone o repositório.
2. Configure o `appsettings.json` na raiz da pasta `Route53DDns`.
3. Restaure as dependências:
   ```bash
   dotnet restore
   ```
4. Build do projeto:
   ```bash
   dotnet build -c Release
   ```
5. Rode a aplicação:
   ```bash
   dotnet run --project Route53DDns/Route53DDns.csproj
   ```

### Execução via Docker

A aplicação possui um `Dockerfile` multi-estágio para otimização de imagem.

1. Build da imagem:
   ```bash
   docker build -t route53-ddns .
   ```
2. Rodar o container:
   ```bash
   docker run -d \
     -v ${PWD}/Route53DDns/appsettings.json:/app/appsettings.json \
     --name ddns-updater \
     route53-ddns
   ```

### Execução via Docker Compose (Recomendado)

O uso do Docker Compose é a forma mais prática de configurar a aplicação via variáveis de ambiente.

1. Edite o arquivo `docker-compose.yml` preenchendo as variáveis de ambiente necessárias.
2. Suba o serviço:
   ```bash
   docker-compose up -d
   ```
3. Acompanhe os logs:
   ```bash
   docker-compose logs -f
   ```

## 🧪 Testes

O projeto conta com uma suíte de testes unitários cobrindo a lógica de configuração, validação de IP e orquestração do Route 53.

Para rodar os testes:
```bash
dotnet test
```

## 🔍 Funcionamento na Inicialização

Ao iniciar, o serviço realiza as seguintes ações:
1. Valida as configurações fornecidas.
2. Busca a Hosted Zone informada na AWS.
3. Lista e exibe no console todos os registros atuais encontrados na zona para fins informativos.
4. Inicia o loop de monitoramento do IP externo.

## 🔐 Segurança e Permissões

Recomenda-se criar um usuário IAM com permissões mínimas. Exemplo de política necessária:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "route53:ListHostedZones",
                "route53:ListResourceRecordSets",
                "route53:ChangeResourceRecordSets"
            ],
            "Resource": "*"
        }
    ]
}
```
