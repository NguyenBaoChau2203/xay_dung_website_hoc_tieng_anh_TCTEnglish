document.getElementById('readingForm').addEventListener('submit', async function (e) {
    e.preventDefault();
    const btn = document.getElementById('btnSubmit');
    const formData = new FormData(this);
    btn.disabled = true;

    const response = await fetch('/Reading/SubmitReading', { method: 'POST', body: formData });
    const result = await response.json();

    if (result.success) {
        document.getElementById('quizStatus').className = "alert alert-success d-block";
        document.getElementById('quizStatus').innerHTML = `<h5>Kết quả: ${result.correctCount}/${result.totalCount}</h5>`;

        result.details.forEach(item => {
            const card = document.querySelector(`.question-card[data-q-id="${item.questionId}"]`);
            const feedback = card.querySelector('.result-feedback');
            feedback.classList.remove('d-none');

            if (item.isCorrect) {
                card.classList.add('border-success', 'bg-light-success');
                feedback.innerHTML = '<b class="text-success">✓ Đúng</b>';
            } else {
                card.classList.add('border-danger', 'bg-light-danger');
                feedback.innerHTML = `<b class="text-danger">✘ Sai.</b> Đáp án: <span class="text-success">${item.correctOptionText}</span>`;
            }
            card.querySelectorAll('input').forEach(i => i.disabled = true);
        });
        btn.classList.add('d-none');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }
});