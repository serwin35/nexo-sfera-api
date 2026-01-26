-- =============================================================================
-- FIX: TransakcjaVAT Column Order Mismatch for InsERT Nexo SDK v59
-- =============================================================================
--
-- PROBLEM:
-- The SDK v59 EF6 model expects columns in this order:
--   Index 27: WystepowanieFakturKsef
--   Index 28: IdWInstancji
--   Index 29: TimeStamp
--
-- But the database has columns in this order:
--   Ordinal 27: IdWInstancji
--   Ordinal 28: TimeStamp
--   Ordinal 32: WystepowanieFakturKsef (added at the end during migration)
--
-- This causes: "The 'IdWInstancji' property on 'TransakcjaVAT' could not be
-- set to a 'System.Byte[]' value" error during PZ document creation.
--
-- SOLUTION:
-- Recreate the table with columns in the correct order.
--
-- WARNING:
-- - BACKUP YOUR DATABASE BEFORE RUNNING THIS SCRIPT!
-- - Run during maintenance window when no users are connected
-- - Test on a copy of the database first
-- - This script modifies critical ERP data - proceed with caution
-- =============================================================================

USE [Nexo_DMservice]; -- Change to your database name
GO

-- Step 0: Check current column order
PRINT 'Current column order in TransakcjeVAT:';
SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'ModelDanychContainer' AND TABLE_NAME = 'TransakcjeVAT'
ORDER BY ORDINAL_POSITION;
GO

-- Step 1: Check foreign key constraints on the table
PRINT 'Foreign keys referencing TransakcjeVAT:';
SELECT
    fk.name AS FK_Name,
    tp.name AS Parent_Table,
    cp.name AS Parent_Column,
    tr.name AS Referenced_Table,
    cr.name AS Referenced_Column
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tr.name = 'TransakcjeVAT' AND SCHEMA_NAME(tr.schema_id) = 'ModelDanychContainer';
GO

-- =============================================================================
-- MANUAL FIX STEPS (run step by step, not all at once):
-- =============================================================================

/*
-- Step 2: Create backup of the table
SELECT * INTO [ModelDanychContainer].[TransakcjeVAT_BACKUP_20260126]
FROM [ModelDanychContainer].[TransakcjeVAT];

-- Verify backup
SELECT COUNT(*) as BackupRowCount FROM [ModelDanychContainer].[TransakcjeVAT_BACKUP_20260126];
SELECT COUNT(*) as OriginalRowCount FROM [ModelDanychContainer].[TransakcjeVAT];

-- Step 3: Generate FK recreation scripts (RUN THIS AND SAVE OUTPUT!)
-- This generates the exact ALTER TABLE statements needed to recreate FKs
SELECT
    'ALTER TABLE [' + SCHEMA_NAME(tp.schema_id) + '].[' + tp.name + '] ADD CONSTRAINT [' + fk.name + ']' + CHAR(13) + CHAR(10) +
    '    FOREIGN KEY ([' + cp.name + ']) REFERENCES [' + SCHEMA_NAME(tr.schema_id) + '].[' + tr.name + '] ([' + cr.name + ']);' AS DropScript
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tr.name = 'TransakcjeVAT' AND SCHEMA_NAME(tr.schema_id) = 'ModelDanychContainer';
GO

-- IMPORTANT: Copy the output from above and save it before proceeding!

-- Step 4: Drop ALL 22 foreign key constraints referencing this table
-- These were identified from the database:

ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedaz];
ALTER TABLE [ModelDanychContainer].[RejestrVAT] DROP CONSTRAINT [FK_TransakcjaVATRejestrVAT];
ALTER TABLE [ModelDanychContainer].[DaneZapisuBadzPozycji] DROP CONSTRAINT [FK_TransakcjaVATDaneZapisuBadzPozycji];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakup];
ALTER TABLE [ModelDanychContainer].[TransakcjeVAT] DROP CONSTRAINT [FK_TransakcjaVATPowiazana];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazFakturyDomyslne];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupFakturyDomyslne];
ALTER TABLE [ModelDanychContainer].[TransakcjeVAT] DROP CONSTRAINT [FK_TransakcjaVATRejestrVATTransakcjiPowiazanej];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupKorekty];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazKorekty];
ALTER TABLE [ModelDanychContainer].[RodzajeDokumentowHandlowych] DROP CONSTRAINT [FK_RodzajDokumentuHandlowegoTransakcjaVAT];
ALTER TABLE [ModelDanychContainer].[RodzajeDokumentowHandlowych] DROP CONSTRAINT [FK_RodzajDokumentuHandlowegoTransakcjaVATKorekta];
ALTER TABLE [ModelDanychContainer].[ElementyZestawieniaVAT] DROP CONSTRAINT [FK_ElementZestawieniaVATTransakcjaVAT];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotoweNabycie];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotowaDostawaDomyslne];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotoweNabycieDomyslne];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotowaDostawa];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazDomyslneTrybFiskalny];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupEwidencjaTrybFiskalny];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazEwidencjaTrybFiskalny];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupDomyslneTrybFiskalny];
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] DROP CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWNTDomyslneTrybFiskalny];

-- Step 5: Create new table with correct column order
-- The key is to place WystepowanieFakturKsef BEFORE IdWInstancji and TimeStamp

CREATE TABLE [ModelDanychContainer].[TransakcjeVAT_NEW] (
    [Id] int NOT NULL,
    [PanstwoKonsumpcji_Id] int NULL,
    [Nazwa] nvarchar(256) NOT NULL,
    [Dotyczy] tinyint NOT NULL,
    [OstrzezenieBrakNIP] bit NOT NULL,
    [OstrzezenieBrakNIPUE] bit NOT NULL,
    [MiesiacNaliczenia] tinyint NOT NULL,
    [TworzZapisPowiazany] bit NOT NULL,
    [Symbol] nvarchar(10) NOT NULL,
    [PowiazanyTypNIP] tinyint NOT NULL,
    [CzyZmodyfikowano] bit NOT NULL,
    [RodzajZakupu] tinyint NULL,
    [RodzajOdliczeniaZakupu] tinyint NULL,
    [Aktywna] bit NOT NULL,
    [CelZakupu] tinyint NULL,
    [PokazOknoZapisuPodczasAktualizacji] bit NOT NULL,
    [DataWystawieniaZapisuSkojarzonego] tinyint NULL,
    [DataSprzedazyOtrzymaniaZapisuSkojarzonego] tinyint NULL,
    [DataZakonczeniaDostawyZapisuSkojarzonego] tinyint NULL,
    [RodzajDatyMiesiacaNaliczenia] tinyint NOT NULL,
    [GrupyTowarowIUslugJPK] int NULL,
    [ProceduryJPK] int NOT NULL,
    [TypDokumentuJPK] tinyint NOT NULL,
    [DotyczyEwidencjiVATOSS] bit NOT NULL,
    [DomyslnieWidocznePolskieStawkiVAT] bit NOT NULL,
    [StatusKsiegowyZapisuSkojarzonego] tinyint NULL,
    [OstrzezenieBrakSME] bit NOT NULL,
    -- IMPORTANT: WystepowanieFakturKsef moved HERE (before IdWInstancji and TimeStamp)
    [WystepowanieFakturKsef] tinyint NULL,
    [IdWInstancji] int NULL,
    [TimeStamp] timestamp NOT NULL,
    [TransakcjaPowiazana_Id] int NULL,
    [RejestrTransakcjiPowiazanej_Id] int NULL,
    [Instancja_Id] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TransakcjeVAT_NEW] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Step 6: Copy data to new table (excluding TimeStamp which auto-generates)
INSERT INTO [ModelDanychContainer].[TransakcjeVAT_NEW] (
    [Id], [PanstwoKonsumpcji_Id], [Nazwa], [Dotyczy], [OstrzezenieBrakNIP],
    [OstrzezenieBrakNIPUE], [MiesiacNaliczenia], [TworzZapisPowiazany], [Symbol],
    [PowiazanyTypNIP], [CzyZmodyfikowano], [RodzajZakupu], [RodzajOdliczeniaZakupu],
    [Aktywna], [CelZakupu], [PokazOknoZapisuPodczasAktualizacji],
    [DataWystawieniaZapisuSkojarzonego], [DataSprzedazyOtrzymaniaZapisuSkojarzonego],
    [DataZakonczeniaDostawyZapisuSkojarzonego], [RodzajDatyMiesiacaNaliczenia],
    [GrupyTowarowIUslugJPK], [ProceduryJPK], [TypDokumentuJPK], [DotyczyEwidencjiVATOSS],
    [DomyslnieWidocznePolskieStawkiVAT], [StatusKsiegowyZapisuSkojarzonego],
    [OstrzezenieBrakSME], [WystepowanieFakturKsef], [IdWInstancji],
    [TransakcjaPowiazana_Id], [RejestrTransakcjiPowiazanej_Id], [Instancja_Id]
)
SELECT
    [Id], [PanstwoKonsumpcji_Id], [Nazwa], [Dotyczy], [OstrzezenieBrakNIP],
    [OstrzezenieBrakNIPUE], [MiesiacNaliczenia], [TworzZapisPowiazany], [Symbol],
    [PowiazanyTypNIP], [CzyZmodyfikowano], [RodzajZakupu], [RodzajOdliczeniaZakupu],
    [Aktywna], [CelZakupu], [PokazOknoZapisuPodczasAktualizacji],
    [DataWystawieniaZapisuSkojarzonego], [DataSprzedazyOtrzymaniaZapisuSkojarzonego],
    [DataZakonczeniaDostawyZapisuSkojarzonego], [RodzajDatyMiesiacaNaliczenia],
    [GrupyTowarowIUslugJPK], [ProceduryJPK], [TypDokumentuJPK], [DotyczyEwidencjiVATOSS],
    [DomyslnieWidocznePolskieStawkiVAT], [StatusKsiegowyZapisuSkojarzonego],
    [OstrzezenieBrakSME], [WystepowanieFakturKsef], [IdWInstancji],
    [TransakcjaPowiazana_Id], [RejestrTransakcjiPowiazanej_Id], [Instancja_Id]
FROM [ModelDanychContainer].[TransakcjeVAT];

-- Verify row counts match
SELECT COUNT(*) as NewTableRowCount FROM [ModelDanychContainer].[TransakcjeVAT_NEW];
SELECT COUNT(*) as OriginalRowCount FROM [ModelDanychContainer].[TransakcjeVAT];

-- Step 7: Drop original table and rename new one
-- WARNING: Make sure all foreign keys are dropped first!
DROP TABLE [ModelDanychContainer].[TransakcjeVAT];
EXEC sp_rename 'ModelDanychContainer.TransakcjeVAT_NEW', 'TransakcjeVAT';
EXEC sp_rename 'ModelDanychContainer.PK_TransakcjeVAT_NEW', 'PK_TransakcjeVAT';

-- Step 8: Recreate all 22 foreign key constraints
-- NOTE: You may need to adjust these based on actual column names - verify with SSMS

ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedaz]
    FOREIGN KEY ([TransakcjaVATSprzedaz_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[RejestrVAT] ADD CONSTRAINT [FK_TransakcjaVATRejestrVAT]
    FOREIGN KEY ([TransakcjaVAT_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[DaneZapisuBadzPozycji] ADD CONSTRAINT [FK_TransakcjaVATDaneZapisuBadzPozycji]
    FOREIGN KEY ([TransakcjaVAT_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakup]
    FOREIGN KEY ([TransakcjaVATZakup_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[TransakcjeVAT] ADD CONSTRAINT [FK_TransakcjaVATPowiazana]
    FOREIGN KEY ([TransakcjaPowiazana_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazFakturyDomyslne]
    FOREIGN KEY ([TransakcjaVATSprzedazFakturyDomyslne_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupFakturyDomyslne]
    FOREIGN KEY ([TransakcjaVATZakupFakturyDomyslne_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[TransakcjeVAT] ADD CONSTRAINT [FK_TransakcjaVATRejestrVATTransakcjiPowiazanej]
    FOREIGN KEY ([RejestrTransakcjiPowiazanej_Id]) REFERENCES [ModelDanychContainer].[RejestrVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupKorekty]
    FOREIGN KEY ([TransakcjaVATZakupKorekty_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazKorekty]
    FOREIGN KEY ([TransakcjaVATSprzedazKorekty_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[RodzajeDokumentowHandlowych] ADD CONSTRAINT [FK_RodzajDokumentuHandlowegoTransakcjaVAT]
    FOREIGN KEY ([TransakcjaVAT_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[RodzajeDokumentowHandlowych] ADD CONSTRAINT [FK_RodzajDokumentuHandlowegoTransakcjaVATKorekta]
    FOREIGN KEY ([TransakcjaVATKorekta_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ElementyZestawieniaVAT] ADD CONSTRAINT [FK_ElementZestawieniaVATTransakcjaVAT]
    FOREIGN KEY ([TransakcjaVAT_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotoweNabycie]
    FOREIGN KEY ([TransakcjaVATWewnatrzwspolnotoweNabycie_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotowaDostawaDomyslne]
    FOREIGN KEY ([TransakcjaVATWewnatrzwspolnotowaDostawaDomyslne_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotoweNabycieDomyslne]
    FOREIGN KEY ([TransakcjaVATWewnatrzwspolnotoweNabycieDomyslne_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWewnatrzwspolnotowaDostawa]
    FOREIGN KEY ([TransakcjaVATWewnatrzwspolnotowaDostawa_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazDomyslneTrybFiskalny]
    FOREIGN KEY ([TransakcjaVATSprzedazDomyslneTrybFiskalny_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupEwidencjaTrybFiskalny]
    FOREIGN KEY ([TransakcjaVATZakupEwidencjaTrybFiskalny_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATSprzedazEwidencjaTrybFiskalny]
    FOREIGN KEY ([TransakcjaVATSprzedazEwidencjaTrybFiskalny_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATZakupDomyslneTrybFiskalny]
    FOREIGN KEY ([TransakcjaVATZakupDomyslneTrybFiskalny_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);
ALTER TABLE [ModelDanychContainer].[ParametryKsiegowosci] ADD CONSTRAINT [FK_ParametrKsiegowosciTransakcjaVATWNTDomyslneTrybFiskalny]
    FOREIGN KEY ([TransakcjaVATWNTDomyslneTrybFiskalny_Id]) REFERENCES [ModelDanychContainer].[TransakcjeVAT] ([Id]);

-- Step 9: Verify new column order
SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'ModelDanychContainer' AND TABLE_NAME = 'TransakcjeVAT'
ORDER BY ORDINAL_POSITION;
*/

-- =============================================================================
-- ALTERNATIVE: Contact InsERT Support
-- =============================================================================
--
-- The safest solution is to contact InsERT support and ask:
-- 1. Is there an official database migration script for SDK v59?
-- 2. Should the WystepowanieFakturKsef column be in a specific position?
-- 3. Can you provide the correct database schema for SDK v59?
--
-- InsERT Support: https://www.insert.com.pl/
-- =============================================================================
