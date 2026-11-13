using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text;
using System.Threading.Tasks; // Fix: Add missing using for Parallel

namespace CSharpPlayground.Advanced.Thread
{
    internal class Thread
    {
        public void RunParallelCounter()
        {
            int counter = 0;
            Parallel.For(0, 1_000_000, _ => counter++);
            Console.WriteLine(counter);
        }
    }
}

/*
      บทที่ 1.1 รากฐาน: Thread, ThreadPool และ Race Condition
         แนวคิด
         Thread คือหน่วยการรันบน OS, สร้างแพง (ประมาณ 1 MB stack ต่อ thread)
         ThreadPool คือกลุ่ม thread ที่ใช้ซ้ำ, Task.Run ส่งงานเข้า pool
         Race condition คือผลลัพธ์ขึ้นกับจังหวะการสลับ thread
         แยกให้ออกระหว่าง
             Data race: หลาย thread เข้าถึงตัวแปรเดียวกัน อย่างน้อยหนึ่งตัวเขียน โดยไม่มีการซิงก์
             Race condition เชิง logic: เช่น check-then-act (เช็กว่าว่างแล้วค่อยจอง)  ซึ่งเกิดได้แม้ทุก  operation จะ thread-safe ก็ตาม
         Visibility: thread หนึ่งเขียนค่า อีก thread อาจไม่เห็นทันทีเพราะ CPU cache และการoptimizeของJIT
*/