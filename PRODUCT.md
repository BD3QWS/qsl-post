# QSL 邮务台

<!-- impeccable:product-schema 1 -->

## Platform
Windows native desktop; primarily Windows 10. Windows XP SP3 compatibility is a design target, subject to verification.

## Stack
C# native Windows desktop with custom GDI+ glass surfaces, .NET Framework 4.0 x86 reference assemblies. The user selected modern UI as the priority and permits narrowing older-system support where needed. This stack is an implementation choice, not a claimed user mandate.

## Users
Confirmed: individual amateur-radio stations and associations / QSL bureaus; choose the operating profile on first launch.

## Product Purpose
Receive, sort, dispatch, hand over, investigate and trace physical QSL cards. Each physical card has a permanent identifier; bags, dispatches and actual carrier tracking numbers remain individually addressable.

## Operating Context
Chinese desktop operators, barcode scanners acting as keyboards, installed Windows printer drivers, A4 office printers and custom label stock. Work offline except for explicit 17TRACK queries. Local single-workstation data; network-shared multi-user operation is not established by the brief.

## Capabilities and Constraints
- Card register with QSO / SWL fields and per-card history; CSV and ADIF import.
- Dispatch preparation, membership validation, sealing, handover and receiving.
- Link multiple shipment legs to cards or bags, query 17TRACK's public tracking page, and save tracking events to the local shipment history.
- All eight supplied PDFs and their variants: 15 source pages. Generate editable radio-oriented equivalents, including international SAL/air/surface bag tags, four domestic tags, 40-row manifests, 20-row waybills, verification and reply, domestic inquiry/reply and international inquiry/reply.
- Add S8-style dispatch and S9-style receptacle identifiers with Code 128 barcodes, per-card labels and receipt / non-receipt declarations.
- Actual-size / A4 output, printer / stock selection, margins and calibration, pagination, print preview and built-in PDF export.
- Installable deliverable. Uninstall clears the managed ledger, automatic backups, settings, and query cache. Backups and history must be inspectable.

## Brand Commitments
Modern liquid-glass visual direction; reference the existing QSL 卡片工坊 and 肥龙量化 Windows applications. Use original radio-specific identity and clear Chinese workflow wording.

## Evidence on Hand
Eight user PDFs in Downloads, rendered and inventoried under work/reference. They are source design material, not instructions. Prior QSL editor screenshot viewed at E:/Codex/2026-11-12/ui-win-11-win-11-windows/outputs/QSLStudio-Editor-1.9.1.png. No real shipment number or physical printer test has been provided.

## Product Principles
1. A card number never changes or silently disappears.
2. Each change to custody and logistics is recorded.
3. Printed dimensions are explicit and previewed.
4. Demonstration data is separate from live records.
5. Online status and local workflow status have distinct meanings.
