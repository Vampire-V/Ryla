# Blueprint: Project "Ryla" (SaaS Automation 2026)

---

### 1. คอนเซปต์หลัก (Core Concept)
* **ชื่อโปรเจกต์:** **Ryla** (มาจากคำว่า "ไหล" - Data Flow)
* **รูปแบบ:** **Automation & Integration Wrapper** (Micro-SaaS)
* **จุดประสงค์:** เป็น "กาว" เชื่อมต่อระบบ (Glue Service) โดยเน้นตลาดเฉพาะกลุ่มในไทย เช่น เชื่อม TikTok Shop, Shopee เข้ากับ LINE OA และ Google Sheets

---

### 2. เทคโนโลยีที่เลือกใช้ (Tech Stack - Updated)
* **Backend (.NET 10 - LTS):**
    * **High Stability:** ใช้รุ่น Long Term Support เพื่อความมั่นใจในระยะยาว
    * **Enhanced Native AOT:** ใช้การ Compile แบบ Native เพื่อให้ Cold Start บน Serverless เร็วที่สุด (เกือบ Instant)
    * **AI-Ready:** ใช้ Library ใหม่ใน .NET 10 ที่จัดการเรื่อง Semantic Kernel และ AI Orchestration ได้ง่ายขึ้น
* **Frontend (Next.js - App Router):**
    * เน้นความเร็วสูงสุดและ **SEO** เพื่อดึงดูดลูกค้า
    * ใช้ **Edge Runtime** เพื่อจัดการเรื่อง **GEO-Location** ของผู้ใช้ทั่วโลก
* **Database & Auth:**
    * **Supabase (PostgreSQL):** จัดการ Data และ Auth แบบรวดเร็วและประหยัดต้นทุน

---

### 3. โครงสร้างพื้นฐานและต้นทุน (Infrastructure & Cost)
* **Serverless Optimized:** รัน .NET 10 บน **Azure Functions** (Consumption Plan) ต้นทุนเกือบเป็น 0 ในช่วงเริ่มต้น
* **Global Edge:** Deploy Frontend บน **Vercel** หรือ **Cloudflare** เพื่อความเร็วระดับมิลลิวินาทีจากทุกที่

---

### 4. กลยุทธ์การเติบโต (Growth Strategy)
* **Market Fit:** เจาะจงแก้ปัญหาให้ SME ไทยที่ต้องการ Automation ในราคาที่ถูกกว่า Zapier
* **Trust & Performance:** ชูจุดเด่นเรื่องความเสถียรของเทคโนโลยี Microsoft (.NET 10) และความเร็วของ Next.js

---

### 5. แผนการพัฒนาขั้นแรก (MVP Milestone)
* **First Pipe:** รับ Webhook จากแพลตฟอร์มปลายทาง -> ประมวลผลด้วย .NET 10 -> ส่งแจ้งเตือนเข้า LINE ให้สำเร็จใน 48 ชั่วโมง