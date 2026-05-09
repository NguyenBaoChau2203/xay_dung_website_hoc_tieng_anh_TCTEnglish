import sys

path = r'd:\repo\TCTEnglish\Views\Shared\_Layout.cshtml'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

c = c.replace('<aside class="sidebar">', '@if (!hideSidebar)\n        {\n        <aside class="sidebar">', 1)

old_footer = '''</aside>

        <!-- ================= MAIN ================= -->'''

new_footer = '''</aside>
        }

        <!-- ================= MAIN ================= -->'''

c = c.replace(old_footer, new_footer, 1)

# Also handle CRLF if the above replace didn't work due to \r\n
old_footer_crlf = '</aside>\n\n        <!-- ================= MAIN ================= -->'
new_footer_crlf = '</aside>\n        }\n\n        <!-- ================= MAIN ================= -->'
c = c.replace(old_footer_crlf, new_footer_crlf, 1)

old_footer_rn = '</aside>\r\n\r\n        <!-- ================= MAIN ================= -->'
new_footer_rn = '</aside>\r\n        }\r\n\r\n        <!-- ================= MAIN ================= -->'
c = c.replace(old_footer_rn, new_footer_rn, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)

print('Done')
