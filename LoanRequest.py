import sys

class LoanRequest:

    def __init__(self, customer_id, amount, months):
        self.customer_id = customer_id
        self.amount = amount
        self.months = months

    def monthly_payment(self):
        return self.amount / self.months


d = LoanRequest(1, 5000, 12)



print(d.monthly_payment())
print(sys.executable)