<#
.SYNOPSIS
    Script de diagnostico para testes de conexao SQL Server e listagem de bancos.
.DESCRIPTION
    Este script testa a conectividade TCP de rede, a conexao via SqlClient e a permissao de listagem de bancos.
.EXAMPLE
    .\Test-SqlServerConnection.ps1
.EXAMPLE
    .\Test-SqlServerConnection.ps1 -Server "SRVLOGIMAT\MLSQLEXPRESS" -User "mearsulink" -Password "Me@sur1ink"
#>

param(
    [string]$Server = "SRVLOGIMAT,56623\MLSQLEXPRESS",
    [string]$User = "mearsulink",
    [string]$Password = "Me@sur1ink",
    [string]$Database = "master"
)

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "       DIAGNOSTICO DE CONEXAO SQL SERVER - SelectML   " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "Servidor: $Server" -ForegroundColor White
Write-Host "Usuario:  $User" -ForegroundColor White
Write-Host "Banco:    $Database" -ForegroundColor White
Write-Host "------------------------------------------------------" -ForegroundColor Gray

# 1. Teste de Conectividade TCP/Porta
if ($Server -match ",(\d+)") {
    $port = $Matches[1]
    $hostName = ($Server -split ",")[0]
    Write-Host "`n[1/3] Testando conectividade de rede na porta TCP $port em $hostName..." -ForegroundColor Yellow
    
    try {
        $netTest = Test-NetConnection -ComputerName $hostName -Port $port -WarningAction SilentlyContinue
        if ($netTest.TcpTestSucceeded) {
            Write-Host " -> SUCESSO: Porta TCP $port esta acessivel!" -ForegroundColor Green
        } else {
            Write-Host " -> FALHA: Nao foi possivel conectar na porta TCP $port em $hostName. Verifique o Firewall." -ForegroundColor Red
        }
    } catch {
        Write-Host " -> Aviso: Nao foi possivel executar Test-NetConnection." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "`n[1/3] Nenhuma porta TCP explicita encontrada na string. Pulando teste de porta." -ForegroundColor Gray
}

# 2. Teste de Autenticacao SQL Server
Write-Host "`n[2/3] Tentando autenticar e abrir conexao com a base '$Database'..." -ForegroundColor Yellow

$connStr = "Server=$Server;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;Encrypt=False;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $conn.Open()
    Write-Host " -> SUCESSO: Conexao autenticada e estabelecida com sucesso!" -ForegroundColor Green
    
    # 3. Teste de Listagem de Bases de Dados
    Write-Host "`n[3/3] Consultando lista de bases de dados visiveis..." -ForegroundColor Yellow
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')"
    $reader = $cmd.ExecuteReader()
    
    $databasesFound = @()
    while ($reader.Read()) {
        $databasesFound += $reader.GetString(0)
    }
    
    if ($databasesFound.Count -gt 0) {
        Write-Host " -> Bases encontradas ($($databasesFound.Count)):" -ForegroundColor Green
        foreach ($db in $databasesFound) {
            Write-Host "    - $db" -ForegroundColor White
        }
    } else {
        Write-Host " -> Nenhum banco de usuario encontrado ou permissao restrita." -ForegroundColor DarkYellow
    }
    
    $conn.Close()
    Write-Host "`n======================================================" -ForegroundColor Cyan
    Write-Host " DIAGNOSTICO CONCLUIDO: Conexao OK!" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Cyan
}
catch {
    Write-Host "`n -> FALHA NA CONEXAO:" -ForegroundColor Red
    Write-Host "    $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception -is [System.Data.SqlClient.SqlException]) {
        $sqlEx = $_.Exception
        Write-Host "`n --- Detalhes do Erro SQL Server ---" -ForegroundColor Yellow
        Write-Host " Number (Codigo): $($sqlEx.Number)" -ForegroundColor White
        Write-Host " State (Estado):  $($sqlEx.State)" -ForegroundColor White
        Write-Host " Class (Severidade): $($sqlEx.Class)" -ForegroundColor White
        
        Write-Host "`n Dica de Diagnostico:" -ForegroundColor Cyan
        if ($sqlEx.Number -eq 18456) {
            if ($sqlEx.State -eq 5) {
                Write-Host " -> Estado 5: O usuario '$User' nao existe nesta instancia SQL Server." -ForegroundColor Yellow
            } elseif ($sqlEx.State -eq 8) {
                Write-Host " -> Estado 8: Senha incorreta." -ForegroundColor Yellow
            } elseif ($sqlEx.State -eq 11 -or $sqlEx.State -eq 12) {
                Write-Host " -> Estado 11/12: Login desabilitado no SQL Server." -ForegroundColor Yellow
            } elseif ($sqlEx.State -eq 38) {
                Write-Host " -> Estado 38: O banco de dados especifico ($Database) nao pode ser acessado pelo usuario." -ForegroundColor Yellow
            } else {
                Write-Host " -> Login falhou no SQL Server. Verifique usuario, senha e banco padrao." -ForegroundColor Yellow
            }
        } elseif ($sqlEx.Number -eq 53) {
            Write-Host " -> Nao foi possivel localizar/conectar no servidor. Verifique o nome do servidor, IP ou Firewall." -ForegroundColor Yellow
        } elseif ($sqlEx.Number -eq 26) {
            Write-Host " -> Erro ao localizar servidor/instancia especificada." -ForegroundColor Yellow
        } else {
            Write-Host " -> Verifique a mensagem do erro acima." -ForegroundColor Yellow
        }
    }
    Write-Host "`n======================================================" -ForegroundColor Cyan
    Write-Host " DIAGNOSTICO CONCLUIDO: Erro Detectado" -ForegroundColor Red
    Write-Host "======================================================" -ForegroundColor Cyan
}
