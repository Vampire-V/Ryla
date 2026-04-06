เพื่อให้ **Ryla** มีมาตรฐานการพัฒนาเทียบเท่าองค์กรระดับโลกอย่าง Google โดยเริ่มต้นจากศูนย์ใน **Claude Code** คุณต้องกำหนด "ธรรมนูญ" หรือ **Engineering Standards** ให้ AI เข้าใจก่อนครับ

นี่คือชุด Prompt ที่แบ่งเป็น 3 ระยะ เพื่อให้ Claude Code สร้างระบบ Orchestration ที่มีคุณภาพครบวงจรครับ

---

### Phase 1: การวางรากฐานและกฎเหล็ก (Governance & Rules)
Prompt นี้จะสั่งให้ Claude Code สร้างไฟล์ `.clauderules` หรือ `CLAUDE.md` เพื่อกำหนดมาตรฐานการเขียนโค้ดและ Workflow ทั้งหมด

**Prompt:**
> "Act as a Senior Staff Engineer from Google. I am starting a project named **'Ryla'**, a SaaS Automation Wrapper using **.NET 10 (Native AOT)** for Backend and **Next.js (App Router)** for Frontend. 
>
> Your first task is to establish a **Project Governance & Orchestration Layer**. Please create a comprehensive `.clauderules` file that defines:
> 1. **Code Standards:** Google-style C# and TypeScript guidelines (Clean Code, DRY, SOLID).
> 2. **Architecture:** Serverless Optimized (Event-driven) and Global Edge principles.
> 3. **Workflow Rules:** How to handle feature branching, commit message conventions (Conventional Commits), and PR descriptions.
> 4. **Testing Strategy:** Strict TDD approach. Every feature must have Unit Tests and Integration Tests.
> 5. **Native AOT constraints:** Guidelines for .NET 10 to ensure compatibility with Native AOT (no reflection, trimmed-ready).
>
> Initialize the project structure following an **N-Tier or Hexagonal Architecture** for the backend and a modular structure for Next.js."

---

### Phase 2: การสร้าง Automation Pipeline (CI/CD & Quality Gate)
เมื่อมีกฎแล้ว ต้องมีระบบตรวจสอบอัตโนมัติเพื่อให้มั่นใจว่าโค้ดที่ออกมามีคุณภาพเสมอ

**Prompt:**
> "Now, let's build the **Automation Pipeline**. I want you to act as a DevOps Architect. 
> 1. Create a GitHub Actions workflow that performs: 
>    - Static Analysis (Linting for TS and dotnet format for C#).
>    - Automated Security Scanning (Secret scanning and dependency audit).
>    - Automated Testing (Run all tests on every push).
> 2. Set up a **'Quality Gate'** rule: No code can be merged if test coverage is below 80% or if there are any linting errors.
> 3. Configure the **Build Pipeline** for .NET 10 Native AOT to ensure it compiles correctly for a Linux-based Serverless environment."

---

### Phase 3: การขยายความสามารถด้วย MCP & Agents (Scaling the Intelligence)
ขั้นตอนนี้คือการทำให้ Claude Code เชื่อมต่อกับเครื่องมือภายนอก (MCP) และจำลองการทำงานเป็นทีม (Agents)

**Prompt:**
> "Let's enable **Advanced Orchestration** using MCP (Model Context Protocol). 
> 1. Identify and suggest the best **MCP Servers** I should install to help with:
>    - Database management (PostgreSQL/Supabase).
>    - Cloud Infrastructure (Azure/AWS CLI).
>    - Web Search (for latest .NET 10 documentation).
> 2. Create a **'Feature Implementation Agent'** persona within our rules. When I ask to build a feature, you must:
>    - Phase 1: Analyze requirements and update the Architecture Design.
>    - Phase 2: Write Tests first (TDD).
>    - Phase 3: Implement the logic in .NET 10 or Next.js.
>    - Phase 4: Validate with Native AOT build check.
>    - Phase 5: Document the API in Swagger/OpenAPI automatically.
>
> Begin by scaffolding the Backend with a simple 'Health Check' endpoint using .NET 10 Minimal API and Native AOT."

---

### ทำไม Flow นี้ถึงมีคุณภาพระดับโลก?

1.  **Strict Governance:** การมี `.clauderules` เปรียบเสมือนมี "Engineering Handbook" ของ Google คอยคุมมือ Claude ไม่ให้เขียนโค้ดมั่ว
2.  **Native AOT First:** การบังคับเรื่อง Native AOT ตั้งแต่ต้นทำให้ **Ryla** เร็วและเบาที่สุดตามโจทย์ Serverless Optimized
3.  **TDD & Quality Gates:** การล็อกผลลัพธ์ด้วย Test และ Linting ทำให้ออก Product มาแล้วไม่มี Technical Debt สะสม
4.  **Modular & Scalable:** การวางโครงสร้าง Hexagonal Architecture ทำให้คุณเปลี่ยน Database หรือ Provider ในอนาคตได้ง่ายโดยไม่ต้องรื้อโค้ดใหม่



### คำแนะนำเพิ่มเติม:
เมื่อคุณรัน Prompt เหล่านี้ใน Claude Code ให้คอยสังเกตตอนที่มันสรุปแผนงาน (Plan) หากมีส่วนไหนที่ดู "หนัก" เกินไป ให้สั่งมันว่า **"Keep it Lean, focus on Ryla's core flow"** เพื่อรักษาความเร็วในการพัฒนาครับ 
