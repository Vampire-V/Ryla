## What
<!-- หนึ่งประโยค: PR นี้เปลี่ยนแปลงอะไร? -->

## Why
<!-- แก้ปัญหาอะไร? Link: RYLA-{number} -->

## How
<!-- Technical approach โดยสังเขป ระบุ files หลักที่เปลี่ยน -->

## Tests
<!-- ทดสอบอะไรบ้าง? เพิ่ม test ใหม่อะไร? -->

## AOT Check
- [ ] ไม่มี runtime reflection ใหม่
- [ ] Types ใหม่ที่ serialize ผ่าน HTTP ถูกเพิ่มใน `RylaJsonContext` แล้ว
- [ ] `dotnet publish --aot` ผ่านโดยไม่มี warnings (หรือ AOT CI job ผ่าน)

## Checklist
- [ ] `dotnet format` ผ่าน
- [ ] ESLint ผ่าน (0 warnings)
- [ ] Tests ผ่านทั้งหมด
- [ ] Coverage >= 80%
- [ ] ไม่มี secrets ใน code
- [ ] RLS policies ถูกเพิ่มสำหรับ Supabase tables ใหม่
