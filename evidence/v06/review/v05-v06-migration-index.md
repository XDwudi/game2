# v0.5 → v0.6 配置迁移索引

此表是原样配置差异索引，不是对原规格的修改。完整逐字段差异见 `v05-v06-config-diff.json`。

- 递归差异总数：528；列表按稳定 ID 比较。
- 新控制器 C41–C53；C04/C16/C23 退役且不复用 ID。
- 服务 O05–O07 新增；I05 名称更新但稳定 ID 保留。

| 配置域 | 递归差异项 |
|---|---:|
| activeContentCount | 1 |
| baseControllerSellPrice | 1 |
| colorDefinitions | 1 |
| configVersion | 1 |
| consumables | 8 |
| controllerOrder | 1 |
| controllerSellPrice | 1 |
| controllers | 347 |
| debug | 24 |
| diceUpgrades | 7 |
| display | 1 |
| enchantRules | 16 |
| enchantments | 9 |
| generation | 6 |
| handDefinitions | 45 |
| initialGold | 1 |
| interestBalanceTiming | 1 |
| maxColorLevel | 1 |
| minDiceForColorHand | 1 |
| minDiscountedItemPrice | 1 |
| presentation | 5 |
| redRewards | 1 |
| removedControllerIds | 1 |
| rng | 1 |
| rounding | 1 |
| saveSchemaVersion | 1 |
| scoreStorage | 1 |
| services | 15 |
| shop | 23 |
| stageFlow | 3 |
| stages | 1 |
| walletStorage | 1 |

## controllers

| ID | 旧价 | 新价 | 修改字段 |
|---|---:|---:|---|
| C01 | 8 | 4 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, scoreEnchantEligible, sellPrice, trigger |
| C02 | 14 | 5 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C03 | 6 | 2 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C04 | 8 | — | 退役 |
| C05 | 12 | 5 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C06 | 14 | 4 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, offerWeight, rarity, salePriceMode, sellPrice |
| C07 | 6 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C08 | 10 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, salePriceMode, sellPrice |
| C09 | 14 | 6 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C10 | 5 | 3 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C11 | 10 | 4 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C12 | 14 | 6 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C13 | 5 | 2 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C14 | 8 | 4 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C15 | 6 | 3 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C16 | 12 | — | 退役 |
| C17 | 7 | 3 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C18 | 8 | 3 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C19 | 5 | 2 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C20 | 6 | 2 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C21 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, salePriceMode, sellPrice |
| C22 | 12 | 5 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, salePriceMode, sellPrice |
| C23 | 7 | — | 退役 |
| C24 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C25 | 14 | 6 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C26 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C27 | 6 | 3 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C28 | 10 | 4 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C29 | 10 | 4 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C30 | 14 | 5 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C31 | 12 | 5 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C32 | 10 | 4 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C33 | 9 | 6 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, offerWeight, parameters, rarity, salePriceMode, sellPrice |
| C34 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C35 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C36 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C37 | 8 | 3 | baseSellPrice, buyPrice, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C38 | 6 | 2 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C39 | 6 | 3 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C40 | 9 | 4 | baseSellPrice, buyPrice, description, maxOwned, maxOwnedNormal, parameters, salePriceMode, sellPrice |
| C41 | — | 6 | 新增 |
| C42 | — | 5 | 新增 |
| C43 | — | 5 | 新增 |
| C44 | — | 6 | 新增 |
| C45 | — | 4 | 新增 |
| C46 | — | 5 | 新增 |
| C47 | — | 6 | 新增 |
| C48 | — | 3 | 新增 |
| C49 | — | 3 | 新增 |
| C50 | — | 3 | 新增 |
| C51 | — | 4 | 新增 |
| C52 | — | 4 | 新增 |
| C53 | — | 4 | 新增 |

## consumables

| ID | 旧价 | 新价 | 修改字段 |
|---|---:|---:|---|
| I01 | 5 | 2 | baseBuyPrice |
| I02 | 4 | 2 | baseBuyPrice |
| I03 | 6 | 3 | baseBuyPrice |
| I04 | 3 | 1 | baseBuyPrice |
| I05 | 3 | 1 | baseBuyPrice, displayName |
| I06 | 5 | 2 | baseBuyPrice |
| I07 | 5 | 2 | baseBuyPrice |

## services

| ID | 旧价 | 新价 | 修改字段 |
|---|---:|---:|---|
| O01 | 5 | 2 | baseBuyPrice, description, priceRule |
| O02 | 7 | 3 | baseBuyPrice, description, priceRule |
| O03 | 0 | 3 | baseBuyPrice, description, priceRule |
| O04 | 3 | 1 | baseBuyPrice, description, priceRule |
| O05 | — | 3 | 新增 |
| O06 | — | 2 | 新增 |
| O07 | — | 6 | 新增 |

## diceUpgrades

| ID | 旧价 | 新价 | 修改字段 |
|---|---:|---:|---|
| U01 | 3 | 1 | buyPrice |
| U02 | 3 | 1 | buyPrice |
| U03 | 3 | 1 | buyPrice |
| U04 | 3 | 1 | buyPrice |
| U05 | 2 | 1 | buyPrice |
| U06 | 5 | 2 | buyPrice |
| U07 | 2 | 1 | buyPrice |

## handDefinitions

| ID | 旧价 | 新价 | 修改字段 |
|---|---:|---:|---|
| all_blue | 6 | — | 退役 |
| all_red | 5 | — | 退役 |
| eight_kind | 12 | 5 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| five_kind | 6 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| four_pair | — | 4 | 新增 |
| full_house | 5 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| pair | 6 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| quads | 5 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| seven_kind | 10 | 4 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| six_kind | 8 | 3 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| straight | 5 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| three_pair | — | 3 | 新增 |
| trips | 6 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| two_pair | 6 | 2 | baseUpgradePrice, enabledInNormalShop, structuralPriority, tieBreakRank |
| two_trips | — | 3 | 新增 |
