using System.Collections.Concurrent;

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

int counter = 0;
Parallel.For(0, 1_000_000, _ => counter++);
Console.WriteLine(counter);

// Parallel ใน C# หมายถึง “ให้ทำงานหลายอย่างพร้อมกัน” โดยใช้หลาย core ของ CPU

// Parallel.For จะสร้าง thread ขึ้นมาหลายตัวเพื่อรัน loop พร้อมกัน ทำให้เกิด race condition ขึ้นระหว่าง thread ต่างๆ ที่เข้าถึงตัวแปร counter พร้อมกัน
// ทำไม ถึงไม่ได้ 1000000? เพราะเกิด race condition ขึ้นระหว่าง thread ต่างๆ ที่เข้าถึงตัวแปร counter พร้อมกัน


var seats = new ConcurrentDictionary<int, string>();
int success = 0;

await Task.WhenAll(Enumerable.Range(0, 100).Select(i => Task.Run(() =>
{
    if (!seats.ContainsKey(1))      // เช็ก
    {
        seats[1] = $"user{i}";      // แล้วค่อยจอง
        Interlocked.Increment(ref success);
    }
})));

Console.WriteLine($"จองสำเร็จ {success} คน"); // ควรเป็น 1 แต่มักได้มากกว่า