import pandas as pd
import matplotlib.pyplot as plt

#import os
#print(os.getcwd())

df = pd.read_csv('assignment_data.csv')

print(df)

seizure_positive = df.query('y == 1')
seizure_negative = df.query('y == 0')
#grouped = df.groupby(["y"])

print(seizure_negative.loc[[0, 1, 2]])

fig, ax = plt.subplots()

ax.plot(seizure_negative.loc[0])

_ = ax.set(
    title="Seizure positive vs negative",
    xticks=[i * 100 for i in range(30)],
    xlabel="milliseconds",
    ylabel="I don't know"
)