# Конфигурация
$groupName = "DaprWorkshop"
$inboundPorts = @(3600, 3601, 3602, 6000, 6001, 6002, 60000, 60001, 60002, 4025)
$outboundPorts = @(3600, 3601, 3602, 6000, 6001, 6002, 60000, 60001, 60002, 4025)

# Функция создания правила
function New-FirewallRuleForPort {
    param (
        [string]$Direction, # 'Inbound' или 'Outbound'
        [int]$Port
    )

    $ruleName = "$groupName-$($Direction.ToLower())-$Port"

    # Проверка на существование (чтобы не создавать повторно)
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule `
            -DisplayName $ruleName `
            -Direction $Direction `
            -LocalPort $Port `
            -Protocol TCP `
            -Action Allow `
            -Enabled True `
            -Profile Any `
            -Group $groupName
        Write-Host "Rule created: $ruleName" -ForegroundColor Green
    } else {
        Write-Host "Rule already exists: $ruleName" -ForegroundColor Red
    }
}

# Создаём inbound правила
foreach ($port in $inboundPorts) {
    New-FirewallRuleForPort -Direction "Inbound" -Port $port
}

# Создаём outbound правила
foreach ($port in $outboundPorts) {
    New-FirewallRuleForPort -Direction "Outbound" -Port $port
}

# Вывод всех правил этой группы с деталями
Write-Host "Rules in group '$groupName':" -ForegroundColor Cyan

Get-NetFirewallRule -Group $groupName | ForEach-Object {
    $rule = $_
    $pf = Get-NetFirewallPortFilter -AssociatedNetFirewallRule $rule -ErrorAction SilentlyContinue

    [PSCustomObject]@{
        Name        = $rule.Name
        DisplayName = $rule.DisplayName
        Direction   = $rule.Direction
        Port        = $pf.LocalPort
        Protocol    = $pf.Protocol
        Action      = $rule.Action
        Enabled     = $rule.Enabled
        Group       = $rule.Group
    }
} | Format-Table -AutoSize