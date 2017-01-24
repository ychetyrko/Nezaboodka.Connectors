use `nz_test_closure`;

#========================================
# User and Group types -----------------------------

call before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('User', '_user', NULL),
('Group', '_group', NULL);
call add_all_types();

# User and Group fields ---------------------------

call before_alter_fields();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
('Login', '_login', 'User', 'VARCHAR(60)', FALSE, 'IgnoreCase', NULL),
('Title', '_title', 'Group', 'VARCHAR(255)', FALSE, 'None', NULL),
('Participants', '_participants', 'Group', 'User', TRUE, 'None', 'Group'),
('DescriptionText', '_description_text', 'Group', 'TEXT', FALSE, 'IgnoreCase', NULL);
call add_all_fields();

#========================================
# Admin and Moderator types -------------

call before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('Admin', '_admin', 'User'),
('Moderator', '_moderator', 'User');
call add_all_types();

# Admin and Moderator fields ------------

call before_alter_fields();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
#Admin
('ControlGroup', '_control_group', 'Admin', 'Group', FALSE, 'None', 'Admin'),
#Moderator
('ModeratedGroup', '_moderated_group', 'Moderator', 'Group', FALSE, 'None', 'Admin');
call add_all_fields();

#========================================
# UberAdmin type -------------

call before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUE
('UberAdmin', '_uber_admin', 'Admin');
call add_all_types();

# UberAdmin fields ------------

call before_alter_fields();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
('UberRating', '_uber_rating', 'UberAdmin', 'INT', FALSE, 'None', NULl);
call add_all_fields();
